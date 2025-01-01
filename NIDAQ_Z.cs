using System;
using NationalInstruments.DAQmx;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Threading.Tasks.Sources;

namespace NIDAQ_Zstage
    {
    public class NIDAQ_Z : Stage
        {
        // to be connected to PCI-6251 NIDAQMX card to control Z stage of the microscope
        private readonly SerialPort CommInterface = new SerialPort();

        //private readonly NationalInstruments.DAQmx.Task myTask;
        private readonly NationalInstruments.DAQmx.TaskAction dTask;
        private readonly NationalInstruments.DAQmx.Device device;
        private readonly NationalInstruments.DAQmx.DaqSystem daqSys;
        private AnalogEdgeStartTriggerSlope triggerSlope;       // not sure how to use this yet!
        //private readonly NationalInstruments.DAQmx.

        //private KCubeDCServo device;      // k cube specific ; 
        //private MotorConfiguration motorConfiguration;    //; yet to find equivalent for NIDAQmx stage
        //private DCMotorSettings currentDeviceSettings;    //; yet to find equivalent for NIDAQmx stage
        //private MotorDirection direction;                 //; yet to find equivalent for NIDAQmx stage
        public string DevID { get; private set; } = "";
        public string[] DevList { get; private set; } = [];


        private readonly double _minPositionMm = 0;     // unsure about the values and param of the actual Z stage
        private readonly double _maxPositionMm = 10;
        private readonly double _maxVelocityMms = 2.5;
        private readonly string _units = "mm";
        public double minPositionMm { get { return _minPositionMm; } }
        public double maxPositionMm { get { return _maxPositionMm; } }
        public double maxVelocityMms { get { return _maxVelocityMms; } }
        public string units { get { return _units; } }


        public event EventHandler<PositionUpdatedEventArgs> PositionUpdated;

        public event EventHandler<StageErrorEventArgs> ErrorOccured;

        public bool IsConnected { get; private set; } = false;
        public int activeAxis { get; private set; } = 0;
        public int axisCount { get { return _axisCount; } }
        public readonly int _axisCount = 1;
        public int axisIndexStart { get; private set; } = 0;
        public double CurrentPosition
            {
            get
                {
                lock (CurrentPositionLock) { return _CurrentPosition; }
                }
            private set
                {
                lock (CurrentPositionLock) { _CurrentPosition = value; }
                }
            }
        private double _CurrentPosition;
        private readonly object CurrentPositionLock = new object();   // need further looking into

        public StageControllerStatus CurrentStatus { get; private set; }

        /// <summary>
        /// /////////////////////////////////////////////////////////////////////////////////
        /// </summary>

        NationalInstruments.DAQmx.Task myTask = new NationalInstruments.DAQmx.Task(); // may be redundant given myTask is created up top

        public void Connect(string serialNumber)
            {
            if (IsConnected)
                {
                throw new InvalidOperationException("The stage controller is already connected.");
                }

            long SN = device.SerialNumber;
            serialNumber = SN.ToString();
            DevID = device.DeviceID;
            DevList = myTask.Devices;
          

            // Open a connection to the device.
            try
                {
                //device.ReserveNetworkDevice();
                //daqSys.ConnectTerminals();
                AOChannel myAOChannel;
                myAOChannel = myTask.AOChannels.CreateVoltageChannel("dev1/ai0","myAOChannel",0,5,AOVoltageUnits.Volts);

                }
            catch (Exception)
                {
                // Connection failed
                throw new InvalidOperationException("Failed to connect to the device.");
                }

            // Wait for the device settings to initialize - timeout 5000ms
            if (!device.IsSettingsInitialized())
                {
                try
                    {
                    device.WaitForSettingsInitialized(5000);
                    }
                catch (Exception)
                    {
                    throw new InvalidOperationException("Settings failed to initialize");
                    }
                }
            Thread.Sleep(250);
            myTask.Start();
            Thread.Sleep(250);

            // Yet to find equivalent to modify this section 
            //motorConfiguration = device.LoadMotorConfiguration(serialNumber);
            //motorConfiguration.DeviceSettingsName = "NIDAQmxZstage";
            //motorConfiguration.UpdateCurrentConfiguration();


            currentDeviceSettings = device.MotorDeviceSettings as DCMotorSettings;

            //Home(); //only homes if necessary
            StartControllerMonitoring();        // from within CPTMetrology namespace's classes
            IsConnected = true;
            }

        public void Disconnect()
            {
            if (!IsConnected)
                {
                throw new InvalidOperationException("No stage controller is connected.");
                }

            if (myTask != null)
                {
                myTask.Stop();
                myTask.Dispose();
                device.Dispose();
                //daqSys.DisconnectAll();
                //dTask.Abort;
                }
            IsConnected = false;

            }       // may need more work?

        public void AbsoluteMove(double position)
            {
            if (!IsConnected)
                {
                throw new InvalidOperationException("No stage controller is connected.");
                }

            try
                {
                device.SetMoveAbsolutePosition(Convert.ToDecimal(position));
                device.MoveAbsolute(60000);
                }
            catch (DeviceNotReadyException)
                {
                throw new DeviceNotReadyException(device.DeviceID, "Device is not ready. Not properly handled yet.");
                }
            catch (DeviceSettingsException)
                {
                throw new DeviceSettingsException(device.DeviceID, "Device Settings Exception from AbsoluteMove.");
                }

            }       // yet to change

        public void RelativeMove(double increment)
            {
            if (!IsConnected)
                {
                throw new InvalidOperationException("No stage controller is connected.");
                }

            Decimal relativeMove = Convert.ToDecimal(increment);
            Decimal relativeMoveAbs = Math.Abs(relativeMove);

            if (relativeMove < 0)
                {
                direction = MotorDirection.Backward;
                }
            else
                {
                direction = MotorDirection.Forward;
                }
            try
                {
                //device.SetMoveRelativeDistance(Convert.ToDecimal(increment));
                //device.MoveRelative(60000);
                device.MoveRelative(direction, Convert.ToDecimal(Math.Abs(increment)), 60000);
                }
            catch (DeviceNotReadyException)
                {
                throw new DeviceNotReadyException(device.DeviceID, "Device is not ready. Not properly handled yet.");
                }
            catch (DeviceSettingsException)
                {
                throw new DeviceSettingsException(device.DeviceID, "Device Settings Exception from AbsoluteMove.");
                }
            catch (MoveToInvalidPositionException)
                {
                if ((_CurrentPosition + increment) < _minPositionMm)
                    {
                    AbsoluteMove(_minPositionMm);
                    return;
                    }
                else if ((_CurrentPosition + increment) > _maxPositionMm)
                    {
                    AbsoluteMove(_maxPositionMm);
                    return;
                    }
                }
            }       // yet to change

        public void SetVelocity(double velocity)
            {
            if (!IsConnected)
                {
                throw new InvalidOperationException("No stage controller is connected.");
                }
            VelocityParameters velPars = device.GetVelocityParams();
            velPars.MaxVelocity = Convert.ToDecimal(velocity);
            device.SetVelocityParams(velPars);
            }       // yet to change

        public void SetAxis(int axis)
            {
            throw new NotImplementedException();
            }       // yet to change

        public void Home()
            {
            if (!IsConnected)
                {
                throw new InvalidOperationException("No stage controller is connected. Cannot home.");
                }
            try
                {
                device.Home(60000);
                }
            catch (DeviceNotReadyException)
                {
                throw new DeviceNotReadyException(device.DeviceID, "NIDAQmxZstage.Home()", "Device not ready to home.");
                }
            catch (DeviceNotEnabledException)
                {
                throw new DeviceNotEnabledException(device.DeviceID, "NIDAQmxZstage.Home()", "Cannot home, device not enabled.");
                }
            catch (MoveTimeoutException)
                {
                throw new MoveTimeoutException(device.DeviceID, "NIDAQmxZstage.Home()");
                }


            }       // yet to change

        public void triggerSlopeRising(object sender, System.EventArgs e)
            {
            triggerSlope = AnalogEdgeStartTriggerSlope.Rising;
            }
        public void triggerSlopeFalling(object sender, System.EventArgs e)
            {
            triggerSlope = AnalogEdgeStartTriggerSlope.Falling;
            }

        // Deconstructor, called upon when this obj is deconstructed
        ~NIDAQ_Z()
            {
            if (IsConnected)
                {
                Disconnect();
                }
            }

        private static string[] ErrorDescriptions = new string[]
        {
                "Nagative end of run",
                "Positive end of run",
                "Peak current limit",
                "RMS current limit",
                "Short circuit detection",
                "Following error",
                "Homing time out",
                "Wrong ESP stage",
                "DC voltage too low",
                "80W output power exceeded"
        };

        private static string[] GetControllerErrorDescriptions(int errorCode)
            {
            List<string> desc = new List<string>();
            int compare = 1;
            for (int i = 0; i < 10; ++i)
                {
                if (0 != (errorCode & (compare << i)))
                    {
                    desc.Add(ErrorDescriptions[i]);
                    }
                }
            return desc.ToArray();
            }


        }
    }