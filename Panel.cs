using System.Threading;
using System.Diagnostics;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.I2c;
using ImplicateX.TinyCLR.Drivers.Display;
using ImplicateX.TinyCLR.Drivers.Decoder;
using ImplicateX.TinyCLR.Drivers.Decoder.IR;
using ImplicateX.TinyCLR.Drivers.Expander;
using ImplicateX.TinyCLR.Drivers.Tuner;

namespace THERadioReceiver
{
	/// <summary>
	/// Initializes and manages the radio receiver's front-panel controls, indicators,
	/// display, rotary tuner input, and infrared remote control.
	/// </summary>
	/// <param name="chassis">The receiver chassis providing board and radio services.</param>
	public sealed class Panel( Chassis chassis )
	{
		public Chassis Chassis { get; private set; } = chassis;
		private GpioController gpioController = null!;
		private Mcp23017 gpioExpander = null!;
		private I2cController i2cController = null!;

		/// <summary>Gets the front-panel stereo indicator pin.</summary>
		public GpioPin StereoLedPin { get; private set; } = null!;

		/// <summary>Gets the front-panel tuned indicator pin.</summary>
		public GpioPin TunedLedPin { get; private set; } = null!;

		/// <summary>Gets the front-panel RDS indicator pin.</summary>
		public GpioPin RdsLedPin { get; private set; } = null!;

		/// <summary>Gets the 16x2 vacuum fluorescent display.</summary>
		public Generic16x2VFD VFDisplay { get; private set; } = null!;

		/// <summary>Gets the LS7366 quadrature decoder used for tuning input.</summary>
		public Ls7366 QuadDecoder { get; private set; } = null!;

		/// <summary>Gets the infrared remote-control decoder.</summary>
		public BinaryDistanceDecoder BDDecoder { get; private set; } = null!;

		/// <summary>
		/// Opens and configures the display, front-panel keys and indicators,
		/// quadrature decoder, and infrared remote-control input.
		/// </summary>
		public void Initialize()
		{
			i2cController = Chassis.Board.I2cController;
			gpioController = Chassis.Board.GpioController;
			gpioExpander = Chassis.Board.GpioExpander;

			#region Initialize 16x2 Vacuum Fluorescent Display (VFD)
			VFDisplay = new(
				gpioController,
				vfdClockPinId: Board.ControllerPin.VFDClock,
				vfdDataPinId: Board.ControllerPin.VFDData,
				vfdCSPinId: Board.ControllerPin.VFDCS,
				vfResetPinId: Board.ControllerPin.VFReset );

			VFDisplay.SetScrollDelay( 150 );
			VFDisplay.SetLines( "THE Radio Receiver", "Initializing..." );

			Thread vfdThread = new( () => VFDisplay.Run() )
			{
				Priority = ThreadPriority.Lowest
			};

			vfdThread.Start();
			#endregion

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
			TunedLedPin = gpioExpander.OpenPin( Board.ExpanderPin.LedTuned );
			TunedLedPin.SetDriveMode( GpioPinDriveMode.Output );
			TunedLedPin.Write( GpioPinValue.High );
			StereoLedPin = gpioExpander.OpenPin( Board.ExpanderPin.LedStereo );
			StereoLedPin.SetDriveMode( GpioPinDriveMode.Output );
			StereoLedPin.Write( GpioPinValue.High );
			RdsLedPin = gpioExpander.OpenPin( Board.ExpanderPin.LedRDS );
			RdsLedPin.SetDriveMode( GpioPinDriveMode.Output );
			RdsLedPin.Write( GpioPinValue.High );
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
					Chassis.AudioSystem.RequestAudioSourceToggle();
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
					VFDisplay.Brightness.Value = VFDisplay.Brightness.Value + 0x10;
					Debug.WriteLine( $"Brightness value changed to: {VFDisplay.Brightness.Value}" );
				}
			};
			#endregion

			#region LS7366R Quadrature Decoder/Counter
			QuadDecoder = new(
				gpioController,
				Board.ControllerPin.DecoderCS,
				Board.ControllerPin.DecoderSCLK,
				Board.ControllerPin.DecoderMOSI,
				Board.ControllerPin.DecoderMISO );

			QuadDecoder.CounterChanged += decoderEvent =>
			{
				_ = ( decoderEvent.Direction ) switch
				{
					( Ls7366.Direction.Up ) => Chassis.FMRadio.ApplyTune( true, "Tune Up command received." ),
					( Ls7366.Direction.Down ) => Chassis.FMRadio.ApplyTune( false, "Tune Down command received." ),
					_ => false
				};
			};

			QuadDecoder.ResetCounter();
			QuadDecoder.Start();
			#endregion

			#region Initialize Remote Control IR decoder
			GpioPin remoteCtrlPin = gpioController.OpenPin( Board.ControllerPin.RemoteCtrl );
			remoteCtrlPin.SetDriveMode( GpioPinDriveMode.InputPullUp );
			#endregion

			#region Remote Control IR Binary Distance Decoder
			BDDecoder = new BinaryDistanceDecoder( remoteCtrlPin );
			BDDecoder.FrameDecoded += BDDecoder_FrameDecoded;
			#endregion

		}

		/// <summary>
		/// Handles a decoded infrared command by applying the corresponding tuner
		/// or audio-source action, updating the reported tune frequency, and logging
		/// the command.
		/// </summary>
		/// <param name="arg">The decoded infrared frame, including its raw value.</param>
		private void BDDecoder_FrameDecoded( BinaryDistanceDecoder.FrameEventArgs arg )
		{
			BinaryDistanceDecoder.Code.DENON.TryGetValue( arg.RawValue, out string commandName );

			ushort tuneFrequency10kHz = ( ushort )( Chassis.FMRadio.TuneFrequency );

			_ = ( commandName ) switch
			{
				( "Tuning +" ) => Chassis.FMRadio.ApplyTune( true, "Tune Up command received." ),
				( "Tuning -" ) => Chassis.FMRadio.ApplyTune( false, "Tune Down command received." ),
				( "Tuner Preset +" ) => Chassis.FMRadio.TunerModule.SeekNextStation( Tef668x.SeekDirection.Up, out tuneFrequency10kHz ),
				( "Tuner Preset -" ) => Chassis.FMRadio.TunerModule.SeekNextStation( Tef668x.SeekDirection.Down, out tuneFrequency10kHz ),
				( "Tuner Band" ) => Chassis.AudioSystem.RequestAudioSourceToggle(),
				_ => false
			};

			Chassis.FMRadio.TuneFrequency = tuneFrequency10kHz;

			Debug.WriteLine( $"IR command received: {commandName} (raw: 0x{arg:X4})" );
		}

	}
}
