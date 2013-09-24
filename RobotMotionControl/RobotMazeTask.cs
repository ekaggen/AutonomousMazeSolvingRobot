/*
	Copyright (c) 2012 Eric Kaggen

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace RobotMotionControl
{
	/*
	 * The heart and logic of the robot maze explorer. Contains
	 * all of the high level maze discovery logic. Sends high level commands
	 * to the robot based on its internal state
	 */

	class RobotMazeTask : RobotTask
	{
		public static int LEFT = 0;
		public static int STRAIGHT = 1;
		public static int RIGHT = 2;
		public double intersectionConstant = 2.0;
		private CalibrationControl calibration;
		public int straightPowerHigh;
		public int straightPowerLow;
		public int straightLowPowerZone;
		public double straightKP;
		public double straightKI;
		public double straightKD;
		public double dampingFactor;

		public int rotatePowerHigh;
		public int rotatePowerLow;
		public int rotateLowPowerZone;
		public double rotateKP;
		public double rotateKI;
		public double rotateKD;
		public double rotateSurfConst;

		public int iSectDetectDelay = 3;
		public int initIntersectionsGoal = 0;
		private List<BluetoothPacket> dataPoints = new List<BluetoothPacket>();

		private BackgroundWorker worker;

		private PacketFilter lastPacketFilter;
		public RobotMazeTask(ControlForm gui, CalibrationControl calibration, BluetoothController link)
			: base(gui, link)
		{
			this.calibration = calibration;
		}

		public override void run(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = (BackgroundWorker)sender;
			worker.ReportProgress(0, "Begin robot maze task");
			this.worker = worker;
			mazeSearch();
		}

		// Main maze search method. Performs a DFS search looking for the goal node.
		private void mazeSearch()
		{
			// Init start node with an unknown node to the north.
			MazeNode start = new MazeNode();
			start.explored = true;
			start.neighbors[MazeNode.NORTH] = new MazeNode();
			start.neighbors[MazeNode.NORTH].neighbors[MazeNode.SOUTH] = start;
			MazeEdge pathHead = new MazeEdge(MazeNode.NORTH, start);
			MazeEdge current = pathHead;

			// Move toward the unknown node
			goStraightAsync(0);
			while (running)
			{
				getNextEdge(current);

				if (current.nextEdge != null)
				{
					worker.ReportProgress(3,"Current node(moving away from): id=" + current.node.thisId);
					// Move to next physically
					move(current, current.nextEdge); // Will do nothing if the two directions are the same. Will halt 
					current.direction = current.nextEdge.direction; // Cause its moving toward the next

					current = current.nextEdge;

					// Waits for an intersection
					bool[] isect = getNextIntersection(lastPacketFilter);
					if (isect == null)
					{
						break;
					}

					// Add newly discovered edges
					populateCurrent(current.direction, current.node, isect);
					current.node.explored = true;
				}
				else // need to backtrack
				{
					current.prevEdge.direction = (current.prevEdge.direction + 2) % 4; // Flip previous direction
					//move(current.direction, current.prevEdge.direction); // Should block until robot hits dead end
					current.nextEdge = current.prevEdge; // Backtrack one edge by setting next to the previous edge
					current.prevEdge.nextEdge = null; // Start fresh

					// So now current is sand, facing south. getNextEdge will now return red instead of null like it did previously
					// current = sand; south
					// next = red; north (with no next)
					// 
				}
			}
			Console.WriteLine("MAZE SEARCH ENDED!!!");
			stop();
		}

		private bool robotStopped()
		{
			return false;
			//return this.lastPacketFilter == null || this.lastPacketFilter.Terminated;
		}
		private int move(MazeEdge current, MazeEdge next)
		{
			// Still going forward, could add flag for robot stopped?
			if (current.direction == next.direction && !robotStopped())
				return current.direction;

			int intersectionGoal = 1;
			MazeNode nextNode = next.node;
			for (int i = 0; nextNode != null; i++)
			{
				if (!nextNode.explored)
				{
					intersectionGoal = 0; // Unlimited, til dead end
				}
				nextNode = nextNode.neighbors[next.direction];
			}

			goRotate((next.direction - current.direction + 4) % 4);
			goStraightAsync(intersectionGoal);
			/*
				dest-origin+4 % 4 == 0: no turn
				dest-origin+4 % 4 == 1: left turn -90
				dest-origin+4 % 4 == 2: turn around -180
				dest-origin+4 % 4 == 3: right turn 90
			 */
			return next.direction;
		}

		// Finds the next edge to explore based on the current edge the robot is moving on.
		private void getNextEdge(MazeEdge current)
		{
			if (current.nextEdge != null) return;

			// Search N S E W
			int i = current.direction;
			do
			{
				// If we have an unexplored edge then we have a winner.
				if (current.node.neighbors[i] != null && !current.node.neighbors[i].explored)
				{
					MazeEdge next = new MazeEdge(i, current.node.neighbors[i]);

					next.prevEdge = current;//move
					current.nextEdge = next;//move
					return;
				}
				i = (i + 1) % 4;
			}
			while (i != current.direction);

		}

		// Populates the current MazeNode with the newly discovered intersection
		private void populateCurrent(int direction, MazeNode node, bool[] nextIntersection)
		{
			if (node.explored)
				return;
			if (nextIntersection[0]) // Left
			{
				node.neighbors[(direction + 1) % 4] = new MazeNode();
			}
			if (nextIntersection[1]) // Straight
			{
				node.neighbors[direction] = new MazeNode();
			}
			if (nextIntersection[2]) // Right
			{
				node.neighbors[(direction + 3) % 4] = new MazeNode();
			}

			// Link back
			for (int i = 0; i < 4; i++)
			{
				if (node.neighbors[i] != null)
				{
					node.neighbors[i].neighbors[(i + 2) % 4] = node;
				}
				else
				{
					Console.WriteLine("AHHH YES :)");
				}
			}

		}

		// Sends a rotate command to the robot and returns right away
		// The robot will go forward until it hits a dead end or until
		// it's crossed intersectionGoal intersections. This async call
		// will allow the thread to filter for intersection detected messages.
		// intersectionGoal = 0 for unlimited
		private void goStraightAsync(int intersectionGoal)
		{
			byte[] filterHead = { 0xEE };
			StringBuilder buf = new StringBuilder();
			buf.Append("PID_LINE ");
			buf.Append(intersectionGoal + " ");
			buf.Append(straightPowerHigh + " ");
			buf.Append(straightPowerLow + " ");
			buf.Append(straightLowPowerZone + " ");
			buf.Append(straightKP + " ");
			buf.Append(straightKI + " ");
			buf.Append(straightKD + " ");
			buf.Append(dampingFactor + " ");
			buf.Append(iSectDetectDelay);
			RobotCommand c = new RobotCommand(buf.ToString());
			PacketFilter filter = new PacketFilter(link, filterHead, c.Sequence);
			filter.init();
			link.sendCommand(c);
			this.lastPacketFilter = filter;
		}

		// Sends a rotate command to the robot and waits for the ACK
		private void simplePIDRotate(decimal amount)
		{
			StringBuilder buf = new StringBuilder();
			buf.Append("PID_ROTATE ");
			buf.Append(amount + " ");
			buf.Append(this.rotatePowerHigh + " ");
			buf.Append(this.rotatePowerLow + " ");
			buf.Append(this.rotateLowPowerZone + " ");
			buf.Append(this.rotateKP + " ");
			buf.Append(this.rotateKI + " ");
			buf.Append(this.rotateKD + " ");
			buf.Append(this.rotateSurfConst);
			link.sendCommandAndWait(buf.ToString());
		}

		// Does a PID rotate
		private void goRotate(int type)
		{
			/*
				dest-origin+4 % 4 == 0: no turn
				dest-origin+4 % 4 == 1: left turn -90
				dest-origin+4 % 4 == 2: turn around -180
				dest-origin+4 % 4 == 3: right turn 90
			 */
			if (type == 1)
				simplePIDRotate(-90);
			else if (type == 2)
				simplePIDRotate(180);
			else if (type == 3)
				simplePIDRotate(90);
			else
				return;
		}

		// Blocks until an intersection is reached and then returns the intersection details
		private bool[] getNextIntersection(PacketFilter filter)
		{
			while(true)
			{
				BluetoothPacket p = filter.nextPacket();
				if(p == null) return null;

				if (p.data[1] == 0x44)
				{
					dataPoints.Add(p);
				}
				else if (p.data[1] == 0x66) // Dead end
				{
					bool[] intersectionType = { false, false, false };
					short leftWheel = BitConverter.ToInt16(p.data, 8);
					short rightWheel = BitConverter.ToInt16(p.data, 10);
					worker.ReportProgress(2, "Dead end reached");
					return intersectionType;
				}
				else if (p.data[1] == 0x55)
				{
					ushort left = BitConverter.ToUInt16(p.data, 2);
					ushort center = BitConverter.ToUInt16(p.data, 4);
					ushort right = BitConverter.ToUInt16(p.data, 6);
					short leftWheel = BitConverter.ToInt16(p.data, 8);
					short rightWheel = BitConverter.ToInt16(p.data, 10);
					uint ticks = BitConverter.ToUInt32(p.data, 12);

					ushort deltaTick = BitConverter.ToUInt16(p.data, 16);
					short intersectionError = BitConverter.ToInt16(p.data, 18);
					short intersectionNormError = BitConverter.ToInt16(p.data, 22);

					double intersectionClassifier = (double)intersectionNormError / (double)deltaTick * intersectionConstant;

					bool[] intersectionType = { false, false, false };
					if (intersectionClassifier > 1.0) // Right
					{
						intersectionType[RIGHT] = true;
					}
					else if (intersectionClassifier < -1.0) // Left
					{
						intersectionType[LEFT] = true;
					}
					else
					{
						intersectionType[LEFT] = true;
						intersectionType[RIGHT] = true;
					}

					ushort centerThresh = (ushort)((calibration.SensorsHigh[CalibrationControl.CENTER_SENSOR] +
						calibration.SensorsLow[CalibrationControl.CENTER_SENSOR]) / 2);
					if (center < centerThresh)
					{
						intersectionType[STRAIGHT] = true;
					}

					// Diagnostic / monitoring info
					worker.ReportProgress(2, Environment.NewLine + "==INTERSECTION DETECTED===" + Environment.NewLine +
						"Left Sensor: " + left + Environment.NewLine + "Right Sensor: " + right + Environment.NewLine +
						"Center Sensor," + center + Environment.NewLine + "Left Wheel: " + leftWheel + Environment.NewLine +
						"Right Wheel: " + rightWheel + Environment.NewLine + "Ticks: " + ticks + Environment.NewLine +
						"Intersection Ticks: " + deltaTick + Environment.NewLine + "Intersection Error: " + intersectionError +
						Environment.NewLine + "Equalized Intersection Error: " + intersectionNormError + Environment.NewLine +
						"Classifier Value: " + intersectionClassifier + Environment.NewLine +  "Type: " + (intersectionType[LEFT] ? "L" : "") +
						(intersectionType[STRAIGHT] ? "S" : "") + (intersectionType[RIGHT] ? "R" : "") + Environment.NewLine);
					return intersectionType;
				}
			}
		}
		
		// Informational debugging\calibration method
		private String storedPacketsToString()
		{
			StringBuilder buf = new StringBuilder();
			for (int i = 0; i < dataPoints.Count; i++)
			{
				BluetoothPacket p = dataPoints.ElementAt(i);
				ushort left = BitConverter.ToUInt16(p.data, 2);
				ushort center = BitConverter.ToUInt16(p.data, 4);
				ushort right = BitConverter.ToUInt16(p.data, 6);
				short leftWheel = BitConverter.ToInt16(p.data, 8);
				short rightWheel = BitConverter.ToInt16(p.data, 10);
				uint ticks = BitConverter.ToUInt32(p.data, 12);
				buf.Append(left + "," + center + "," + right + "," + leftWheel + "," + rightWheel + "," + ticks + Environment.NewLine);
				dataPoints.RemoveAt(i);
			}
			return buf.ToString();
		}
		public override void guiUpdate(object sender, ProgressChangedEventArgs e)
		{
			gui.displayLine("" + e.UserState);
		}
	}
}
