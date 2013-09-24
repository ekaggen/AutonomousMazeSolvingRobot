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
	class BluetoothPacket
	{
		public byte[] data;

		public BluetoothPacket(byte[] data)
		{
			this.data = data;
		}

		// Tests if the supplied N byte sequence matches the first N bytes of this packet
		public bool matchesHead(byte[] test)
		{
			if (data.Length < test.Length)
				return false;

			for (int i = 0; i < test.Length; i++)
			{
				if (data[i] != test[i])
					return false;
			}
			return true;
		}

		// Packets are of the form
		// 0xBA, 0xAA {2 to 5}=sequence
		public bool matchesSequence(uint sequence)
		{
			if (data.Length <= 6 || data[0] != 0xBA || data[1] != 0xAA)
				return false;

			byte[] sequenceInt = BitConverter.GetBytes(sequence);
			for (int i = 0; i < 4; i++)
			{
				if (data[i + 2] != sequenceInt[i])
					return false;
			}
			return true;
		}
	}
}
