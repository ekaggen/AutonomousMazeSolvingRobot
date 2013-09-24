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

namespace RobotMotionControl
{
	// A node represending an intersection or dead end.
	// Has neighbors to the north, south, east, and west potentially.
	class MazeNode
	{
		public static int EAST = 0;
		public static int NORTH = 1;
		public static int WEST = 2;
		public static int SOUTH = 3;
		public bool explored = false;
		public MazeNode[] neighbors;

		public static int id = 0;
		public int thisId;
		public MazeNode()
		{
			neighbors = new MazeNode[4];
			explored = false;
			thisId = id++;
			Console.WriteLine("Created new MazeNode with id=" + thisId);
		}
	}
}
