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
using System.Threading;
using System.IO.Ports;

namespace RobotMotionControl
{
	class BluetoothController
	{
		// For the reader thread
		private BackgroundWorker bgWorker;

		// To recieve diagnostics info
		private ControlForm gui;

		// Kill the thread?
		private bool dead = false;

		// BT Connection
		private SerialPort connection;

		// Default ACK timeout
		private int ackTimeout = 3;

		// Requests to wait for a certain type of packet from other threads
		private List<WaitRequest> waitRequests;

		// Unprocessed packet queue
		private List<BluetoothPacket> queue;

		// Default mailbox
		private int mailbox = 0;

		// To block reader thread until next packet comes
		private AutoResetEvent packetWaitEvent;

		public BluetoothController(ControlForm master)
		{
			this.bgWorker = new BackgroundWorker();
			this.bgWorker.DoWork += new DoWorkEventHandler(readLoop);
			this.bgWorker.ProgressChanged += new ProgressChangedEventHandler(readerUpdate);
			this.bgWorker.WorkerReportsProgress = true;
			this.gui = master;

			waitRequests = new List<WaitRequest>();
			queue = new List<BluetoothPacket>();
		}

		// Start the reader thread
		public void startReader()
		{
			this.dead = false;
			this.bgWorker.RunWorkerAsync();
		}

		// Stop reader thread
		public void stopReader()
		{
			this.dead = true;
		}

		// Get rid of a wait request
		public bool removeWaitRequestFromQueue(WaitRequest req)
		{
			bool ret = false;
			lock (queue)
			{
				ret = waitRequests.Remove(req);
			}
			return ret;
		}

		// Helper method to auto-generate a wait request. Will block
		// the calling thread until a matching packet comes in.
		// Returns the first matching packet.
		public BluetoothPacket waitForPacket(byte[] packetHead)
		{
			return waitForPacket(new WaitRequest(packetHead));
		}

		// Waits for a packet using an already generated WaitRequest. Will block
		// the calling thread until a matching packet comes in.
		// Returns the first matching packet.
		public BluetoothPacket waitForPacket(WaitRequest req)
		{
			bool matches = false;

			BluetoothPacket ret = null;
			
			// Acquire a lock on the unprocessed packet queue to determine whether
			// its necessary to block. Might already have a matching packet queued up.
			lock (queue)
			{
				for (int i = 0; i < queue.Count; i++)
				{
					if (queue[i].matchesHead(req.packetHead))
					{
						matches = true;
						ret = queue[i];
						queue.RemoveAt(i);
						break;
					}
				}

				// No matches, add a wait request.
				if (!matches)
				{
					waitRequests.Add(req);
				}
			}
			if (matches) return ret;

			// Block caller
			req.Handle.WaitOne();
			return req.actualPacket;
		}

		// Waits for an ACK packet for the given sequence number (blocking)
		public BluetoothPacket waitForSequence(uint sequence)
		{
			byte[] sequenceInt = BitConverter.GetBytes(sequence);

			byte[] packetHead = { 0xBA, 0xAA, sequenceInt[0], sequenceInt[1], sequenceInt[2], sequenceInt[3] };
			return waitForPacket(packetHead);
		}

		// Sends a bluetooth command and then waits for the ACK
		public BluetoothPacket sendCommandAndWait(String command)
		{
			RobotCommand cmd = new RobotCommand(command);
			sendCommand(cmd);
			return waitForSequence(cmd.Sequence);
		}

		// Sends a command to the specified mailbox on the robot
		public void sendCommand(RobotCommand command, int mailbox)
		{
			byte[] seqBytes = BitConverter.GetBytes(command.Sequence);
			byte[] packet = new byte[command.Command.Length + 9];
			packet[0] = 0x00;
			packet[1] = 0x09;
			packet[2] = (byte)mailbox;
			packet[3] = (byte)(command.Command.Length + 5);

			for (int i = 0; i < 4; i++)
			{
				packet[i + 4] = seqBytes[i];
			}
			for (int i = 0; i < command.Command.Length; i++)
			{
				packet[i + 8] = (byte)command.Command[i];
			}
			packet[command.Command.Length + 8] = 0x00;
			btSend(packet);
		}

		// Helper method to send a command to the default mailbox
		public void sendCommand(RobotCommand command)
		{
			sendCommand(command, this.mailbox);
		}

		// Generates a new command from a string and sends it
		public void sendCommand(String command)
		{
			sendCommand(new RobotCommand(command), this.mailbox);
		}

		// Main packet read loop for reader thread.
		private void readLoop(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = (BackgroundWorker)sender;
			while (true)
			{
				if (!this.connection.IsOpen)
				{
					dead = true;
				}
				if (dead)
				{
					worker.ReportProgress(100, "Bluetooth reader stopped");
					break;
				}

				// Read everything before going to sleep
				while (this.connection.BytesToRead > 0)
				{
					// Read a single packet
					byte[] data = btRead();
					
					BluetoothPacket packet = new BluetoothPacket(data);
					WaitRequest matchedReq = null;

					// Try to match the incoming packet to a wait request
					lock (queue)
					{
						for(int i = 0; i < waitRequests.Count; i++)
						{
							// Stale wait request?
							if (waitRequests[i].Handle == null)
							{
								waitRequests.RemoveAt(i);
							}
							else if (packet.matchesHead(waitRequests[i].packetHead))
							{
								matchedReq = waitRequests[i];
								matchedReq.actualPacket = packet;
								waitRequests.RemoveAt(i);
								break;
							}
						}
						if (matchedReq == null)
						{
							queue.Add(packet);
							//worker.ReportProgress(0, "Bytes recieved " + byteArrayToStr(data));
						}
					}

					// If we matched then wake up the thread waiting on this.
					if (matchedReq != null)
					{
						matchedReq.Handle.Set();
					}
					//worker.ReportProgress(0, "Bytes recieved" + (matchedReq != null ? "* " : " ") + byteArrayToStr(data));
				}

				// Wait for more data
				packetWaitEvent.WaitOne();
			}
		}

		private void dataRecievedHandler(object sender, SerialDataReceivedEventArgs e)
		{
			packetWaitEvent.Set();
		}

		// For diagnostics and monitoring
		private void readerUpdate(object sender, ProgressChangedEventArgs e)
		{
			gui.displayLine("::" + e.UserState);
		}

		// Connects to the bluetooth COM port
		public bool connect(String port)
		{
			this.packetWaitEvent = new AutoResetEvent(false);
			this.connection = new SerialPort();
			this.connection.PortName = port;
			this.connection.ReadTimeout = 3000;
			this.connection.DataReceived += new SerialDataReceivedEventHandler(dataRecievedHandler);
			this.connection.Open();
			startReader();
			return true;
		}

		// Closes the bluetooth connection
		public bool disconnect()
		{
			connection.Close();
			stopReader();
			return true;
		}

		// Sends a byte array over bluetooth
		public void btSend(byte[] command)
		{
			byte[] msgLength = { (byte)command.Length, 0x00 };

			// Send a 2 byte header indicating command length
			connection.Write(msgLength, 0, 2);
			connection.Write(command, 0, command.Length);
		}

		// Sends a byte array over bluetooth and waits for an ACK
		public byte[] btSendAndWait(byte[] command)
		{
			btSend(command);
			return btWaitForAck();
		}

		// Reads a packet off bluetooth
		private byte[] btRead()
		{
			// Read the first 2 byte field to determine the payload size
			int incomingLen = connection.ReadByte() + 256 * connection.ReadByte();

			byte[] incoming = new byte[incomingLen];

			for (int i = 0; i < incomingLen; i++)
			{
				byte b = (byte)connection.ReadByte();
				incoming[i] = b;
			}
			return incoming;
		}

		// Wait for an ACK using the timeout specified in the UI
		public byte[] btWaitForAck()
		{
			return btWaitForAck((int)this.ackTimeout * 1000);
		}

		// Reliable transfer -- blocks until an ACK is recieved (or times out)
		public byte[] btWaitForAck(int timeout) // waits for 0x00 0xAB 0x1C
		{
			int oldTimeout = connection.ReadTimeout;
			connection.ReadTimeout = timeout;

			byte[] response = null;
			while (true)
			{
				try
				{
					response = btRead();
				}
				catch (System.TimeoutException)
				{
					//MessageBox.Show("Command Timed Out!");
					return null;
				}
				if (response[0] == 0x00 && response[1] == 0xAB && response[2] == 0x1C)
				{
					connection.ReadTimeout = oldTimeout;
					connection.DiscardInBuffer();
					return response;
				}

			}
		}

		public static String byteArrayToStr(byte[] bytes)
		{
			StringBuilder b = new StringBuilder();
			for (int i = 0; i < bytes.Length; i++)
			{
				b.Append(bytes[i].ToString("X2") + " ");
			}
			return b.ToString();
		}
		public bool IsOpen
		{
			get
			{
				return this.connection != null && this.connection.IsOpen;
			}
		}
	}
}
