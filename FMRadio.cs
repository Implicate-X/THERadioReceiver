using System.Threading;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.I2c;
using ImplicateX.TinyCLR.Drivers.Expander;
using ImplicateX.TinyCLR.Drivers.Tuner;

namespace THERadioReceiver
{
	public sealed class FMRadio( Chassis chassis )
	{
		public Tef668x FMTuner { get; private set; } = null!;
		private GpioController gpioController = null!;
		private Mcp23017 gpioExpander = null!;
		private I2cController i2cController = null!;
		private GpioPin tunerResPin = null!;
		private GpioPin tunerRdsPin = null!;

		public void Initialize()
		{
			i2cController = chassis.Board.I2cController;
			gpioController = chassis.Board.GpioController;
			gpioExpander = chassis.Board.GpioExpander;

			#region Initialize TEF6686 FM Tuner
			tunerResPin = gpioExpander.OpenPin( Board.ExpanderPin.TunerReset );
			tunerRdsPin = gpioController.OpenPin( Board.ControllerPin.TunerRDS );

			tunerResPin.SetDriveMode( GpioPinDriveMode.Output );
			tunerRdsPin.SetDriveMode( GpioPinDriveMode.InputPullUp );

			FMTuner = new(
				i2cControllerName: FEZFeather.I2cBus.I2c1,
				resPin: tunerResPin,
				rdsPin: tunerRdsPin );

			FMTuner.Initialize();
			FMTuner.SetAudioMute( mute: false );
			FMTuner.TuneToFm( frequency10kHz: tuneFrequency );

			rdsSubSystem = FMTuner.CreateRdsSubSystem( dataAvailableActiveHigh: false );
			rdsSubSystem.Start();

			infoThreadRunning = true;
			infoThread = new Thread( InfoThread );
			infoThread.Start();
			#endregion
		}
	}
}
