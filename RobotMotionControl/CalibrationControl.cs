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
	// Contains basic calibration logic
	class CalibrationControl
	{
		public static int LEFT_SENSOR = 2;
		public static int RIGHT_SENSOR = 0;
		public static int CENTER_SENSOR = 1;

		private ushort[] sensorsLow;
		private ushort[] sensorsHigh;
		private ushort[] threshLow;
		private ushort[] threshHigh;
		private BluetoothController controller;
		public CalibrationControl(BluetoothController controller)
		{
			this.controller = controller;
		}
		public void calibrate()
		{
			// Send calibration packet
			BluetoothPacket calibrationDetails = controller.sendCommandAndWait("CALIBRATE");

			byte[] response = calibrationDetails.data;

			sensorsHigh = new ushort[3];
			sensorsLow = new ushort[3];
			threshHigh = new ushort[3];
			threshLow = new ushort[3];

			sensorsHigh[0] = BitConverter.ToUInt16(calibrationDetails.data, 6);
			sensorsHigh[1] = BitConverter.ToUInt16(calibrationDetails.data, 8);
			sensorsHigh[2] = BitConverter.ToUInt16(calibrationDetails.data, 10);
			sensorsLow[0] = BitConverter.ToUInt16(calibrationDetails.data, 12);
			sensorsLow[1] = BitConverter.ToUInt16(calibrationDetails.data, 14);
			sensorsLow[2] = BitConverter.ToUInt16(calibrationDetails.data, 16);

			for (int i = 0; i < 3; i++)
			{
				threshHigh[i] = (ushort)((sensorsHigh[i]*2 + sensorsLow[i]) / 3);
				threshLow[i] = (ushort)((sensorsHigh[i] + sensorsLow[i]*2) / 3);
			}
		}

		public ushort[] SensorsLow
		{
			get { return sensorsLow; }
		}
		public ushort[] SensorsHigh
		{
			get { return sensorsHigh; }
		}
		public ushort[] ThreshLow
		{
			get { return threshLow; }
		}
		public ushort[] ThreshHigh
		{
			get { return threshHigh; }
		}
	}
}
