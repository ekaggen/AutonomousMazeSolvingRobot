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

namespace RobotMotionControl
{
	// A filter thread to continuously grab packets that match.
	// Automatically generates wait requests for the desired packet.
	class PacketFilter
	{
		// Matched packets
		private List<BluetoothPacket> packets;

		// The bluetooth controller to interact with
		private BluetoothController controller;

		// What packets to filter for
		private byte[] filterType;

		// The blocking wait request for the desired packets
		private WaitRequest filterRequest;

		// Temination packet leading bytes
		private byte[] terminationHead;
		
		private bool waitForSeq = false;
		private uint terminationSeq;

		// To synchronize on new incoming packets
		private AutoResetEvent newItemEvent;

		public bool Terminated { get { return filterRequest.Terminated; } }
		public PacketFilter(BluetoothController controller, byte[] type, byte[] terminationHead)
		{
			this.controller = controller;
			filterType = type;
			this.terminationHead = terminationHead;
		}

		public PacketFilter(BluetoothController controller, byte[] type, uint terminationSeq)
		{
			this.controller = controller;
			filterType = type;
			this.terminationSeq = terminationSeq;
			this.waitForSeq = true;
		}

		public void init()
		{
			packets = new List<BluetoothPacket>();
			filterRequest = new WaitRequest(filterType);

			newItemEvent = new AutoResetEvent(false);
			BackgroundWorker filterWorker = new BackgroundWorker();
			filterWorker.DoWork += new DoWorkEventHandler(filterLoop);

			BackgroundWorker termWorker = new BackgroundWorker();
			termWorker.DoWork += new DoWorkEventHandler(terminationLoop);

			filterWorker.RunWorkerAsync();
			termWorker.RunWorkerAsync();
		}
		public BluetoothPacket nextPacket()
		{
			while (true)
			{
				if (filterRequest.Terminated && packets.Count == 0) return null;
				BluetoothPacket ret = null;
				lock (packets)
				{
					if (packets.Count > 0)
					{
						ret = packets[0];
						packets.RemoveAt(0);
					}
				}
				if (ret != null)
					return ret;
				newItemEvent.WaitOne();
			}
		}

		// Continuously picks up matching packets and then resets the filter.
		private void filterLoop(object sender, DoWorkEventArgs e)
		{
			while (true)
			{
				BluetoothPacket packet = controller.waitForPacket(filterRequest);
				if (filterRequest.Terminated)
				{
					newItemEvent.Set();
					return;
				}
				lock (packets)
				{
					packets.Add(packet);
				}
				newItemEvent.Set();

			}
		}

		// Thread dedicated to picking up packets that match
		// the desired filter termination sequence.
		private void terminationLoop(object sender, DoWorkEventArgs e)
		{
			while (true)
			{
				if (waitForSeq)
				{
					controller.waitForSequence(terminationSeq);
				}
				else
				{
					controller.waitForPacket(terminationHead);
				}
				filterRequest.terminate();
				controller.removeWaitRequestFromQueue(filterRequest);
				return;
			}
		}
	}
}
