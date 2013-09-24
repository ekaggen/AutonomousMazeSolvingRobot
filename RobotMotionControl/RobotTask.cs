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
	// An asynchronous robot control task that happens in a backround thread
	abstract class RobotTask
	{
		protected bool running = false;
		protected ControlForm gui;
		protected BluetoothController link;
		private BackgroundWorker worker;
		public RobotTask(ControlForm gui, BluetoothController link)
		{
			this.gui = gui;
			this.link = link;

			this.worker = new BackgroundWorker();
			this.worker.DoWork += new DoWorkEventHandler(run);
			this.worker.ProgressChanged += new ProgressChangedEventHandler(guiUpdate);
			this.worker.WorkerReportsProgress = true;
		}

		public void start()
		{
			if (!running)
			{
				running = true;
				worker.RunWorkerAsync();
			}
		}
		public void stop()
		{
			this.running = false;
		}

		public bool Running
		{
			get { return running; }
		}
		public abstract void run(object sender, DoWorkEventArgs e);
		public abstract void guiUpdate(object sender, ProgressChangedEventArgs e);
	}
}
