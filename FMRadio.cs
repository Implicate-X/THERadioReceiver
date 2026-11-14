using System;
using System.Threading;
using System.Diagnostics;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.I2c;
using ImplicateX.TinyCLR.Drivers.Expander;
using ImplicateX.TinyCLR.Drivers.Tuner;

namespace THERadioReceiver
{
	/// <summary>
	/// Controls the FM tuner and presents frequency, signal, and RDS information.
	/// </summary>
	/// <remarks>Call <see cref="Initialize"/> to initialize the tuner and start receiving RDS
	/// information.</remarks>
	/// <param name="chassis">The chassis providing the board's I2C and GPIO controllers.</param>
	public sealed class FmRadio( Chassis chassis )
	{
		/// <summary>
		/// Gets the tuner module.
		/// </summary>
		public Tef668x TunerModule { get; private set; } = null!;
		private GpioController gpioController = null!;
		private Mcp23017 gpioExpander = null!;
		private I2cController i2cController = null!;
		private GpioPin tunerResPin = null!;
		private GpioPin tunerRdsPin = null!;
		// 8780, 8880, 9120, 9510 (WDR 3)*
		// 9650, 9810, 9920 (WDR2)*, 10130 (WDR 4)*
		// 10330 (WDRcosmo)*, 10600 (bigFM), 10670 (1LIVE)*
		/// <summary>
		/// Gets or sets the target FM frequency in units of 10 kHz.
		/// </summary>
		/// <remarks>
		/// Changing this property does not tune the radio; use <see cref="ApplyTune"/>
		/// or tune the tuner directly to apply a new frequency.
		/// </remarks>
		public ushort TuneFrequency { get; set; } = 10130;

		/// <summary>
		/// Gets the RDS subsystem.
		/// </summary>
		public Tef668x.RdsSubSystem RdsSubSystem { get; private set; } = null!;
		private bool infoThreadRunning = false;
		/// <summary>
		/// Thread used to retrieve radio information.
		/// </summary>
		private Thread infoThread = null!;
		/// <summary>
		/// Synchronizes access to information-thread state.
		/// </summary>
		private readonly object infoThreadSync = new();
		/// <summary>
		/// Signals that the information thread may resume.
		/// </summary>
		/// <remarks>The event is initially signaled.</remarks>
		private readonly ManualResetEvent infoThreadResume = new( true );
		private bool infoThreadPaused = false;
		/// <summary>
		/// The FM radio display.
		/// </summary>
		private FmRadioDisplay fmRadioDisplay = null!;
		/// <summary>
		/// The minimum score required for a signal to be considered usable.
		/// </summary>
		private const int SignalUsableScoreThreshold = 20;
		private const int SignalLevel1Threshold = 80;
		private const int SignalLevel2Threshold = 130;
		private const int SignalLevel3Threshold = 180;
		private const int SignalLevel4Threshold = 240;
		private const int SignalLevel5Threshold = 320;
		private bool hasTunedSignal = false;
		private bool hasStereoSignal = false;
		private readonly bool EnableDebugOutput = false;
		private DateTime lastSignalDebugOutputUtc = DateTime.MinValue;

		/// <summary>
		/// Initializes and tunes the FM tuner, starts the RDS subsystem, and launches the information thread.
		/// </summary>
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

			TunerModule = new(
				i2cControllerName: FEZFeather.I2cBus.I2c1,
				resPin: tunerResPin,
				rdsPin: tunerRdsPin );

			TunerModule.Initialize();
			TunerModule.SetAudioMute( mute: false );
			TunerModule.TuneToFm( frequency10kHz: TuneFrequency );

			RdsSubSystem = TunerModule.CreateRdsSubSystem( dataAvailableActiveHigh: false );
			RdsSubSystem.Start();
			fmRadioDisplay = new( chassis.Panel.VFDisplay, RdsSubSystem );

			infoThreadRunning = true;
			infoThread = new Thread( InfoThread );
			infoThread.Start();
			#endregion
		}

		/// <summary>
		/// Updates signal indicators and delegates frequency/RDS presentation to the display component.
		/// </summary>
		private void InfoThread()
		{
			const int loopDelayMilliseconds = 200;

			while( infoThreadRunning )
			{
				infoThreadResume.WaitOne();

				lock( infoThreadSync )
				{
					if( infoThreadPaused )
					{
						continue;
					}

					int signalLevel = GetSignalLevel();
					bool hasRdsInfo = fmRadioDisplay.Update( TuneFrequency, signalLevel );
					UpdateIndicatorLeds( hasRdsInfo );
				}

				Thread.Sleep( loopDelayMilliseconds );
			}
		}

		/// <summary>
		/// Gets the current signal level.
		/// </summary>
		/// <returns>The signal level, ranging from 0 (no signal) to 6 (strongest signal).</returns>
		private int GetSignalLevel()
		{
			if( !TunerModule.TryGetFmQualityData( out ushort qualityStatus, out short rfLevelDbuV10, out ushort usn, out ushort wam, out short offsetKhz10, out ushort bandwidthKhz10, out ushort modulation ) )
			{
				hasTunedSignal = false;
				hasStereoSignal = false;
				WriteSignalDebug( qualityStatus: 0, signalStatus: 0, rfLevelDbuV10: 0, hasUsableSignal: false, level: 0, note: "quality data read failed" );
				return 0;
			}

			if( !TunerModule.TryGetFmSignalStatus( out ushort signalStatus ) )
			{
				signalStatus = 0;
			}

			hasStereoSignal = ( signalStatus & 0x8000 ) != 0;
			int qualityTimeStamp = qualityStatus & 0x03FF;
			int score = rfLevelDbuV10 - ( ( usn + wam ) / 2 );
			bool hasUsableSignal = qualityTimeStamp > 0 && score > SignalUsableScoreThreshold;
			hasTunedSignal = hasUsableSignal;

			if( !hasUsableSignal )
			{
				WriteSignalDebug( qualityStatus, signalStatus, rfLevelDbuV10, hasUsableSignal: false, level: 0, note: $"score={score} usn={usn} wam={wam} off={offsetKhz10} bw={bandwidthKhz10} mod={modulation}" );
				return 0;
			}

			int	level = score switch
			{
				< SignalLevel1Threshold => 1,
				< SignalLevel2Threshold => 2,
				< SignalLevel3Threshold => 3,
				< SignalLevel4Threshold => 4,
				< SignalLevel5Threshold => 5,
				_ => 6
			};

			WriteSignalDebug( qualityStatus, signalStatus, rfLevelDbuV10, hasUsableSignal: true, level: level, note: $"score={score} usn={usn} wam={wam} off={offsetKhz10} bw={bandwidthKhz10} mod={modulation}" );
			return level;
		}

		/// <summary>
		/// Writes a debug message with signal information.
		/// </summary>
		/// <param name="qualityStatus">The quality status value.</param>
		/// <param name="signalStatus">The signal status value.</param>
		/// <param name="rfLevelDbuV10">The RF level in dBuV/10.</param>
		/// <param name="hasUsableSignal">Indicates whether the signal is usable.</param>
		/// <param name="level">The signal level.</param>
		/// <param name="note">An optional note to include in the debug message.</param>
		private void WriteSignalDebug( ushort qualityStatus, ushort signalStatus, short rfLevelDbuV10, bool hasUsableSignal, int level, string note = "" )
		{
			if( !EnableDebugOutput )
			{
				return;
			}

			DateTime now = DateTime.UtcNow;

			if( ( now - lastSignalDebugOutputUtc ).TotalMilliseconds < 1000 )
			{
				return;
			}

			lastSignalDebugOutputUtc = now;
			int qualityTimeStamp = qualityStatus & 0x03FF;

			Debug.WriteLineIf( EnableDebugOutput, $"SignalDbg f={TuneFrequency} qs=0x{qualityStatus:X4} ts={qualityTimeStamp} rf={rfLevelDbuV10} s=0x{signalStatus:X4} valid={( hasUsableSignal ? 1 : 0 )} lvl={level} {note}" );
		}

		/// <summary>
		/// Tunes the radio one step up or down and keeps the frequency within the FM band.
		/// </summary>
		/// <param name="up">
		/// <see langword="true"/> to increase the frequency; otherwise, decrease it.
		/// </param>
		/// <param name="logMessage">Message to write to the debug output.</param>
		/// <returns><see langword="true"/> after issuing the tune command.</returns>
		public bool ApplyTune( bool up, string logMessage )
		{
			Debug.WriteLine( logMessage );

			TuneFrequency += ( ushort )( up ? 10 : -10 );

			TuneFrequency = ( TuneFrequency ) switch
			{
				( < 8700 ) => 8700,
				( > 10800 ) => 10800,
				_ => TuneFrequency
			};

			TunerModule.TuneToFm( frequency10kHz: TuneFrequency );

			return true;
		}


		/// <summary>
		/// Updates the tuned, stereo, and RDS indicator LEDs.
		/// </summary>
		/// <param name="hasRdsInfo">
		/// <see langword="true"/> if RDS information is available for the current update.
		/// </param>
		private void UpdateIndicatorLeds( bool hasRdsInfo )
		{
			SetIndicator( chassis.Panel.TunedLedPin, hasTunedSignal );
			SetIndicator( chassis.Panel.StereoLedPin, hasTunedSignal && hasStereoSignal );
			SetIndicator( chassis.Panel.RdsLedPin, hasRdsInfo );
		}

		/// <summary>
		/// Sets an active-low indicator pin to the requested state.
		/// </summary>
		/// <param name="pin">The GPIO pin connected to the indicator.</param>
		/// <param name="isOn">
		/// <see langword="true"/> to turn the indicator on; otherwise, turns it off.
		/// </param>
		private static void SetIndicator( GpioPin pin, bool isOn )
		{
			pin.Write( isOn ? GpioPinValue.Low : GpioPinValue.High );
		}

		/// <summary>
		/// Pauses radio information updates and RDS reception while external audio is active.
		/// </summary>
		/// <remarks>Clears the radio display before returning.</remarks>
		public void PauseForExternalAudio()
		{
			lock( infoThreadSync )
			{
				infoThreadPaused = true;
				infoThreadResume.Reset();
			}

			RdsSubSystem.Stop();
			fmRadioDisplay.Clear();
		}

		/// <summary>
		/// Restarts RDS reception and radio information updates after external audio ends.
		/// </summary>
		public void ResumeAfterExternalAudio()
		{
			RdsSubSystem.Start();

			lock( infoThreadSync )
			{
				infoThreadPaused = false;
				infoThreadResume.Set();
			}
		}

		/// <summary>
		/// Mutes or unmutes the tuner audio output.
		/// </summary>
		/// <param name="mute">
		/// <see langword="true"/> to mute the audio; <see langword="false"/> to unmute it.
		/// </param>
		public void SetAudioMute( bool mute )
		{
			TunerModule?.SetAudioMute( mute );
		}
	}
}
