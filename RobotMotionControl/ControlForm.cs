using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using System.IO;

namespace RobotMotionControl
{
    public partial class ControlForm : Form
    {
		private CalibrationControl calibration = null;
		private RobotTask currentTask = null;
        public const String PROGRAM_FILE = "MotionControl.rxe";
		private BluetoothController btController;
        public ControlForm()
        {
            InitializeComponent();
			btController = new BluetoothController(this);
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            this.connectButton.Enabled = false;
			if (btController.IsOpen) // Disconnecting
            {
				btController.disconnect();
				this.connectButton.Text = "Connect";
				displayLine("Disconnected from Robot");
            }
            else
            {
				if (btController.connect(this.commBox.Text.Trim()))
				{
					this.connectButton.Text = "Disconnect";
					displayLine("Connected to Robot");
				}
                // Enable all buttons?
            }
            this.connectButton.Enabled = true;
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            byte[] packet = new byte[PROGRAM_FILE.Length + 3];
            packet[0] = 0x00;
            packet[1] = 0x00;
            for (int i = 0; i < PROGRAM_FILE.Length; i++)
            {
                packet[i + 2] = (byte)PROGRAM_FILE[i];
            }
            packet[PROGRAM_FILE.Length + 2] = 0x00;
			btController.btSend(packet);
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            byte[] packet = { 0x00, 0x01 };
			btController.btSend(packet);
        }

        private void sendCommand(String command)
        {
			displayLine("-> " + command);
			statusBox.Refresh();
			btController.sendCommandAndWait(command);
        }

        private void goStraightButton_Click(object sender, EventArgs e)
        {
            goStraight(this.simpleStraightAmount.Value);
        }

        private void rotateButton_Click(object sender, EventArgs e)
        {
            goRotate(this.simpleRotateAmount.Value);
        }

        private void doResetTach(byte port, bool relative)
        {
            byte[] packet = { 0x00, 0x0A, port, (byte)(relative ? 0x01 : 0x00) };
			displayLine("Soft resetting Tachometer");
			btController.btSend(packet);
        }

        private void resetTach_Click(object sender, EventArgs e)
        {
			doResetTach(0, false);
			doResetTach(1, false);
        }

        private void sendCommandButton_Click(object sender, EventArgs e)
        {
            sendCommand(commandBox.Text);
        }

        private void goStraight(decimal amount)
        {
            StringBuilder buf = new StringBuilder();
            buf.Append("PID_STRAIGHT ");
            buf.Append(amount + " ");
            buf.Append(this.straightPowerHigh.Value + " ");
            buf.Append(this.straightPowerLow.Value + " ");
            buf.Append(this.straightLowPowerZone.Value + " ");
            buf.Append(this.straightKP.Value + " ");
            buf.Append(this.straightKI.Value + " ");
            buf.Append(this.straightKD.Value);
            sendCommand(buf.ToString());
        }
		private void doLine(decimal amount)
		{
			StringBuilder buf = new StringBuilder();
			buf.Append("PID_LINE ");
			buf.Append(amount + " ");
			buf.Append(this.straightPowerHigh.Value + " ");
			buf.Append(this.straightPowerLow.Value + " ");
			buf.Append(this.straightLowPowerZone.Value + " ");
			buf.Append(this.straightKP.Value + " ");
			buf.Append(this.straightKI.Value + " ");
			buf.Append(this.straightKD.Value + " ");
			buf.Append(this.dampingFactor.Value);
			btController.sendCommand(buf.ToString());
		}
        private void goRotate(decimal amount)
        {
            StringBuilder buf = new StringBuilder();
            buf.Append("PID_ROTATE ");
            buf.Append(amount + " ");
            buf.Append(this.rotatePowerHigh.Value + " ");
            buf.Append(this.rotatePowerLow.Value + " ");
            buf.Append(this.rotateLowPowerZone.Value + " ");
            buf.Append(this.rotateKP.Value + " ");
            buf.Append(this.rotateKI.Value + " ");
			buf.Append(this.rotateKD.Value + " ");
            buf.Append(this.rotateSurfConst.Value);
            sendCommand(buf.ToString());
        }
        private void makeBoxButton_Click(object sender, EventArgs e)
        {
            int rotate = 90;
            if (!this.clockwiseCheckbox.Checked)
            {
                rotate *= -1;
            }

			for (int i = 0; i < this.boxRep.Value; i++)
			{
				goStraight(this.boxLength.Value);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				goRotate(rotate);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				goStraight(this.boxWidth.Value);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				goRotate(rotate);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				goStraight(this.boxLength.Value);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				goRotate(rotate);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				goStraight(this.boxWidth.Value);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				goRotate(rotate);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			}
        }

        private void doLapsButton_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < this.lapsCount.Value; i++)
            {
                goStraight(this.lapsLength.Value);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
                goStraight(-this.lapsLength.Value);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
            }
        }

		private void setRampUp_Click(object sender, EventArgs e)
		{
			sendCommand("SET RAMPUP " + this.rampUpRate.Value);
		}
		public void displayLine(String text)
		{
			this.statusBox.Text += text;
			this.statusBox.Text += Environment.NewLine;
			this.statusBox.Select(statusBox.Text.Length, 0);
			this.statusBox.ScrollToCaret();
		}

		private void pingButton_Click(object sender, EventArgs e)
		{
            /*
			try
			{
				sendCommand("PING");
				btWaitForAck(5000);
				displayLine("Ping reply recieved");
			}
			catch (System.TimeoutException)
			{
				displayLine("Ping timed out");
			}*/
			byte[] num = { 0x55, 0x00, 0x08, 0x53, 0x6E, 0xFF, 0xEF, 0x59, 0x24, 0xB3, 0xDD, 0x38, 0xBA };
			uint a = BitConverter.ToUInt32(num, 1);
			int b = BitConverter.ToInt32(num, 1);
			int c = BitConverter.ToInt32(num, 9);

			Console.WriteLine("Ints: " + a);

		}

		private void doLineButton_Click(object sender, EventArgs e)
		{
			doLine(this.simpleStraightAmount.Value);
		}

		private void calibrateButton_Click(object sender, EventArgs e)
		{
			calibration = new CalibrationControl(btController);
			calibration.calibrate();

			displayLine("Done calibrating");
			displayLine("=> Left Sensor: " + calibration.SensorsLow[CalibrationControl.LEFT_SENSOR] + ", " + calibration.ThreshLow[CalibrationControl.LEFT_SENSOR] + ", " +
				calibration.ThreshHigh[CalibrationControl.LEFT_SENSOR] + ", " + calibration.SensorsHigh[CalibrationControl.LEFT_SENSOR]);
			displayLine("=> Right Sensor: " + calibration.SensorsLow[CalibrationControl.RIGHT_SENSOR] + ", " + calibration.ThreshLow[CalibrationControl.RIGHT_SENSOR] + ", " +
				calibration.ThreshHigh[CalibrationControl.RIGHT_SENSOR] + ", " + calibration.SensorsHigh[CalibrationControl.RIGHT_SENSOR]);
			displayLine("=> Center Sensor: " + calibration.SensorsLow[CalibrationControl.CENTER_SENSOR] + ", " + calibration.ThreshLow[CalibrationControl.CENTER_SENSOR] + ", " +
				calibration.ThreshHigh[CalibrationControl.CENTER_SENSOR] + ", " + calibration.SensorsHigh[CalibrationControl.CENTER_SENSOR]);

		}


		private void runDemo_Click(object sender, EventArgs e)
		{
			if (currentTask != null)
			{
				if (currentTask.Running)
				{
					displayLine("There is already a running task");
					return;
				}
			}
			if (calibration == null)
			{
				//displayLine("You must calibrate before running maze task");
				//return;
			}
			displayLine("Starting run maze task");

			RobotMazeTask task = new RobotMazeTask(this, calibration, btController);

			task.initIntersectionsGoal = Convert.ToInt32(this.intersectionsGoalBox.Value);
			currentTask = task;
			task.straightPowerHigh = Convert.ToInt32(this.straightPowerHigh.Value);
			task.straightPowerLow = Convert.ToInt32(this.straightPowerLow.Value);
			task.straightLowPowerZone = Convert.ToInt32(this.straightLowPowerZone.Value);
			task.straightKP = Convert.ToDouble(this.straightKP.Value);
			task.straightKI = Convert.ToDouble(this.straightKI.Value);
			task.straightKD = Convert.ToDouble(this.straightKD.Value);
			task.dampingFactor = Convert.ToDouble(this.dampingFactor.Value);


			task.rotatePowerHigh = Convert.ToInt32(this.rotatePowerHigh.Value);
			task.rotatePowerLow = Convert.ToInt32(this.rotatePowerLow.Value);
			task.rotateLowPowerZone = Convert.ToInt32(this.rotateLowPowerZone.Value);
			task.rotateKP = Convert.ToDouble(this.rotateKP.Value);
			task.rotateKI = Convert.ToDouble(this.rotateKI.Value);
			task.rotateKD = Convert.ToDouble(this.rotateKD.Value);
			task.rotateSurfConst = Convert.ToDouble(this.rotateSurfConst.Value);

			currentTask.start();
			/*//btConnection.DiscardInBuffer();
			sendCommand("SET COND IL");
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			doLine(150);
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			sendCommand("SET COND NULL");
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			//goStraight(2);
			//btWaitForAck((int)this.ackTimeout.Value * 1000);
			goRotate(-90);
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);

			sendCommand("SET COND D");
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			doLine(30);
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			sendCommand("SET COND NULL");
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			goRotate(180);
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);

			sendCommand("SET COND IR");
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			doLine(30);
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			sendCommand("SET COND NULL");
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			//goStraight(2);
			//btWaitForAck((int)this.ackTimeout.Value * 1000);
			goRotate(90);
			btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			for(int i = 0; i < 3; i++)
			{
				sendCommand("SET COND IR");
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				doLine(30);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				sendCommand("SET COND NULL");
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
				//goStraight(2);
				//btWaitForAck((int)this.ackTimeout.Value * 1000);
				goRotate(90);
				btController.btWaitForAck((int)this.ackTimeout.Value * 1000);
			}*/
		}

		private void haltButton_Click(object sender, EventArgs e)
		{
			if (currentTask != null)
			{
				if (currentTask.Running)
				{
					currentTask.stop();
					displayLine("Halting current task");
				}
			}
			btController.sendCommand(new RobotCommand("HALT"), 1);
		}

		private void rotatePowerHigh_ValueChanged(object sender, EventArgs e)
		{

		}
    }
}
