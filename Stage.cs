using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NIDAQ_Zstage
    {
    public interface Stage
        {
        void Connect(string portName);
        void Disconnect();
        void AbsoluteMove(double position);
        void RelativeMove(double increment);

        void SetVelocity(double velocity);

        void Home();

        void SetAxis(int axis);

        event EventHandler<PositionUpdatedEventArgs> PositionUpdated;
        event EventHandler<StageErrorEventArgs> ErrorOccured;
        bool IsConnected { get; }
        // Unit in mm
        double CurrentPosition { get; }


        double minPositionMm { get; } // currently these three are set statically in each class but would be better in future if they fetched it from the controller upon connection, to support different stage models.
        double maxPositionMm { get; }
        double maxVelocityMms { get; }
        string units { get; }

        /// <summary>
        /// Stores the active axis
        /// </summary>
        int activeAxis { get; }
        int axisIndexStart { get; } // Describes how the stage indexes its axis numbers
        int axisCount { get; }
        StageControllerStatus CurrentStatus { get; }
        }
    }
