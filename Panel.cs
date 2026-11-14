using System.Threading;
using System.Diagnostics;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.I2c;
using ImplicateX.TinyCLR.Drivers.Display;
using ImplicateX.TinyCLR.Drivers.Decoder;
using ImplicateX.TinyCLR.Drivers.Decoder.IR;
using ImplicateX.TinyCLR.Drivers.Expander;

namespace THERadioReceiver
{
	public sealed class Panel( Chassis chassis )
	{
		private GpioController gpioController = null!;
		private Mcp23017 gpioExpander = null!;
		private I2cController i2cController = null!;
		private Ls7366 quadDecoder = null!;
		private Generic16x2VFD vfd = null!;
		private BinaryDistanceDecoder bdd = null!;
		private GpioPin ledStereoPin = null!;
		private GpioPin ledTunedPin = null!;
		private GpioPin ledRdsPin = null!;

		public void Initialize()
		{
			i2cController = chassis.Board.I2cController;
			gpioController = chassis.Board.GpioController;
			gpioExpander = chassis.Board.GpioExpander;

			#region Front panel keys pins
			GpioPin keyMenuPin = gpioExpander.OpenPin( Board.ExpanderPin.KeyMenu );
			keyMenuPin.SetDriveMode( GpioPinDriveMode.InputPullUp );
			GpioPin keyBandPin = gpioExpander.OpenPin( Board.ExpanderPin.KeyBand );
			keyBandPin.SetDriveMode( GpioPinDriveMode.InputPullUp );
			GpioPin keyAutopresetPin = gpioExpander.OpenPin( Board.ExpanderPin.KeyAutopreset );
			keyAutopresetPin.SetDriveMode( GpioPinDriveMode.InputPullUp );
			GpioPin keyDimmerPin = gpioExpander.OpenPin( Board.ExpanderPin.KeyDimmer );
			keyDimmerPin.SetDriveMode( GpioPinDriveMode.InputPullUp );
			#endregion

			#region Front panel LEDs pins
			ledTunedPin = gpioExpander.OpenPin( Board.ExpanderPin.LedTuned );
			ledTunedPin.SetDriveMode( GpioPinDriveMode.Output );
			ledTunedPin.Write( GpioPinValue.High );
			ledStereoPin = gpioExpander.OpenPin( Board.ExpanderPin.LedStereo );
			ledStereoPin.SetDriveMode( GpioPinDriveMode.Output );
			ledStereoPin.Write( GpioPinValue.High );
			ledRdsPin = gpioExpander.OpenPin( Board.ExpanderPin.LedRDS );
			ledRdsPin.SetDriveMode( GpioPinDriveMode.Output );
			ledRdsPin.Write( GpioPinValue.High );
			#endregion

			#region Front panel keys event handlers
			keyMenuPin.ValueChanged += ( sender, args ) =>
			{
				if( args.Edge == GpioPinEdge.FallingEdge )
				{
					Debug.WriteLine( $"Key Menu pin value changed to: {args.Edge}" );
				}
			};

			keyBandPin.ValueChanged += ( sender, args ) =>
			{
				if( args.Edge == GpioPinEdge.FallingEdge )
				{
					RequestAudioSourceToggle();
				}
			};

			keyAutopresetPin.ValueChanged += ( sender, args ) =>
			{
				if( args.Edge == GpioPinEdge.FallingEdge )
				{
					Debug.WriteLine( $"Key Autopreset pin value changed to: {args.Edge}" );
				}
			};

			keyDimmerPin.ValueChanged += ( sender, args ) =>
			{
				if( args.Edge == GpioPinEdge.FallingEdge )
				{
					vfd.Brightness.Value = vfd.Brightness.Value + 0x10;
					Debug.WriteLine( $"Brightness value changed to: {vfd.Brightness.Value}" );
				}
			};
			#endregion

			#region LS7366R Quadrature Decoder/Counter
			quadDecoder = new(
				gpioController,
				Board.ControllerPin.DecoderCS,
				Board.ControllerPin.DecoderSCLK,
				Board.ControllerPin.DecoderMOSI,
				Board.ControllerPin.DecoderMISO );

			quadDecoder.CounterChanged += decoderEvent =>
			{
				_ = ( decoderEvent.Direction ) switch
				{
					( Ls7366.Direction.Up ) => ApplyTune( true, "Tune Up command received." ),
					( Ls7366.Direction.Down ) => ApplyTune( false, "Tune Down command received." ),
					_ => false
				};
			};

			quadDecoder.ResetCounter();
			quadDecoder.Start();
			#endregion

			#region Initialize Remote Control IR decoder
			GpioPin remoteCtrlPin = gpioController.OpenPin( Board.ControllerPin.RemoteCtrl );
			remoteCtrlPin.SetDriveMode( GpioPinDriveMode.InputPullUp );
			#endregion

			#region Remote Control IR Binary Distance Decoder
			bdd = new BinaryDistanceDecoder( remoteCtrlPin );
			bdd.FrameDecoded += Bdd_FrameDecoded;
			#endregion

			#region Initialize 16x2 Vacuum Fluorescent Display (VFD)
			vfd = new(
				gpioController,
				vfdClockPinId: Board.ControllerPin.VFDClock,
				vfdDataPinId: Board.ControllerPin.VFDData,
				vfdCSPinId: Board.ControllerPin.VFDCS,
				vfResetPinId: Board.ControllerPin.VFReset );

			vfd.SetScrollDelay( 150 );
			vfd.SetLines( "THE Radio Receiver", "Initializing..." );

			Thread vfdThread = new( () => vfd.Run() )
			{
				Priority = ThreadPriority.Lowest
			};

			vfdThread.Start();
			#endregion
		}

		/// <summary>
		/// Handles a decoded infrared command by applying the corresponding tuner action and logging the command.
		/// </summary>
		/// <param name="arg">The decoded frame event arguments containing the raw command value.</param>
		private void Bdd_FrameDecoded( BinaryDistanceDecoder.FrameEventArgs arg )
		{
			BinaryDistanceDecoder.Code.DENON.TryGetValue( arg.RawValue, out string commandName );

			ushort tuneFrequency10kHz = ( ushort )( tuneFrequency );

			_ = ( commandName ) switch
			{
				( "Tuning +" ) => ApplyTune( true, "Tune Up command received." ),
				( "Tuning -" ) => ApplyTune( false, "Tune Down command received." ),
				( "Tuner Preset +" ) => tef668x.SeekNextStation( Tef668x.SeekDirection.Up, out tuneFrequency10kHz ),
				( "Tuner Preset -" ) => tef668x.SeekNextStation( Tef668x.SeekDirection.Down, out tuneFrequency10kHz ),
				( "Tuner Band" ) => RequestAudioSourceToggle(),
				_ => false
			};

			tuneFrequency = tuneFrequency10kHz;

			Debug.WriteLine( $"IR command received: {commandName} (raw: 0x{arg:X4})" );
		}

	}
}
