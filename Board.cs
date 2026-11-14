using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.I2c;
using ImplicateX.TinyCLR.Drivers.Tuner;
using ImplicateX.TinyCLR.Drivers.Expander;
using ImplicateX.TinyCLR.Drivers.Decoder;

namespace THERadioReceiver
{
	public sealed class Board( Chassis chassis )
	{
		public static class I2cAddress
		{
			public const byte Expander = 0x20;
			public const byte RTC = 0x52;
			public const byte Tuner = 0x64;
		}

		public static class ControllerPin
		{
			public const int RemoteCtrl = FEZFeather.GpioPin.PA0; // 5
			public const int EthernetCS = FEZFeather.GpioPin.PA3; // 6
			public const int CodecMISO = FEZFeather.GpioPin.PA6; // 7
			public const int CodecMOSI = FEZFeather.GpioPin.PA7; // 8
			public const int CodecSCLK = FEZFeather.GpioPin.PA5; // 9
			public const int PowerSwitch = FEZFeather.GpioPin.PB1; // 10
			public const int EthernetSCLK = FEZFeather.GpioPin.PE12; // 11
			public const int EthernetMOSI = FEZFeather.GpioPin.PE14; // 12
			public const int EthernetMISO = FEZFeather.GpioPin.PE13; // 13
			public const int EthernetINT = FEZFeather.GpioPin.PA10; // 14
			public const int EthernetReset = FEZFeather.GpioPin.PA9; // 15
			public const int DecoderSCLK = FEZFeather.GpioPin.PC8; // 17
			public const int DecoderMOSI = FEZFeather.GpioPin.PC9; // 18
			public const int DecoderMISO = FEZFeather.GpioPin.PC10; // 19
			public const int DecoderCS = FEZFeather.GpioPin.PC11; // 20
			public const int V1INTA = FEZFeather.GpioPin.PC12; // 21
			public const int V1Reset = FEZFeather.GpioPin.PD2; // 22

			public const int TunerRDS = FEZFeather.GpioPin.PE6; // 37
			public const int CodecCS = FEZFeather.GpioPin.PE0; // 36
			public const int CodecDCS = FEZFeather.GpioPin.PE1; // 35
			public const int VFReset = FEZFeather.GpioPin.PD6; // 34
			public const int VFDCS = FEZFeather.GpioPin.PD5; // 33
			public const int VFDData = FEZFeather.GpioPin.PD4; // 32
			public const int VFDClock = FEZFeather.GpioPin.PD3; // 31
			public const int A1SCL = FEZFeather.GpioPin.PB8; // 30
			public const int A1SDA = FEZFeather.GpioPin.PB9; // 29
			public const int IOExtINTA = FEZFeather.GpioPin.PD0; // 28
			public const int IOExtReset = FEZFeather.GpioPin.PD1; // 27
			public const int CodecDREQ = FEZFeather.GpioPin.PE7; // 26
			public const int CodecReset = FEZFeather.GpioPin.PE8; // 25
		}

		public static class ExpanderPin
		{
			public const int KeyMenu = 0;
			public const int KeyAutopreset = 1;
			public const int KeyBand = 2;
			public const int KeyDimmer = 3;
			public const int EncoderA = 5;
			public const int EncoderB = 6;
			public const int EncoderKey = 7;
			public const int CodecCCS = 8;
			public const int V2V3OutputEnable = 9;
			public const int AudioSwitch = 10;
			public const int TunerReset = 11;
			public const int RTCEVI = 12;
			public const int LedRDS = 13;
			public const int LedTuned = 14;
			public const int LedStereo = 15;
		}

		public Mcp23017 GpioExpander { get; private set; } = null!;
		public GpioController GpioController { get; private set; } = null!;
		public I2cController I2cController { get; private set; } = null!;
		private GpioPin audioSwitchPin = null!;
		private readonly AutoResetEvent audioToggleRequest = new( false );
		private readonly object audioToggleSync = new();
		private bool audioTogglePending = false;
		private Thread audioControlThread = null!;
		private bool audioControlThreadRunning = false;
		private readonly List<byte> i2cAdresses = [ I2cAddress.Expander, I2cAddress.RTC, I2cAddress.Tuner ];
		private bool isWebRadioActive = false;

		// 8780, 8880, 9120, 9510 (WDR 3)*
		// 9650, 9810, 9920 (WDR2)*, 10130 (WDR 4)*
		// 10330 (WDRcosmo)*, 10600 (bigFM), 10670 (1LIVE)*
		private ushort tuneFrequency = 10130;
		private Tef668x.RdsSubSystem rdsSubSystem = null!;
		private bool infoThreadRunning = false;
		private Thread infoThread;
		private readonly object infoThreadSync = new();
		private readonly ManualResetEvent infoThreadResume = new( true );
		private bool infoThreadPaused = false;
		private readonly bool EnableDebugOutput = false;
		private DateTime lastSignalDebugOutputUtc = DateTime.MinValue;
		private const int SignalUsableScoreThreshold = 20;
		private const int SignalLevel1Threshold = 80;
		private const int SignalLevel2Threshold = 130;
		private const int SignalLevel3Threshold = 180;
		private const int SignalLevel4Threshold = 240;
		private const int SignalLevel5Threshold = 320;
		private bool hasTunedSignal = false;
		private bool hasStereoSignal = false;

		/// <summary>
		/// Initializes the receiver's hardware interfaces and peripherals, including the I²C devices, controls, display,
		/// tuner, web radio, and audio codec.
		/// </summary>
		/// <remarks>Initialization stops if any expected I²C device is not detected.</remarks>
		public void Initialize()
		{
			GpioController = GpioController.GetDefault();
			I2cController = I2cController.FromName( FEZFeather.I2cBus.I2c1 );

			#region I2C bus scan
			if( !i2cAdresses.TrueForAll( ProbeAddress ) )
			{
				Debug.WriteLine( "Warning: Not all expected I2C devices were found on the bus!" );
				return;
			}
			#endregion

			#region IO Expander MCP23017 Reset sequence
			GpioPin v1ResetPin = gpioController.OpenPin( ControllerPin.V1Reset );
			v1ResetPin.SetDriveMode( GpioPinDriveMode.Output );
			v1ResetPin.Write( GpioPinValue.High );
			Thread.Sleep( 100 );
			v1ResetPin.Write( GpioPinValue.Low );
			Thread.Sleep( 100 );
			v1ResetPin.Write( GpioPinValue.High );
			Thread.Sleep( 100 );
			#endregion

			#region IO Expander MCP23017 initialization
			var mcpDevice = CreateI2cDevice(
				I2cController,
				new I2cConnectionSettings( I2cAddress.Expander, I2cMode.Master, I2cAddressFormat.SevenBit ) );

			/// Interrupt pin for MCP23017 Port A is connected to V1INTA (PD0) on the controller
			var intPin = GpioController.OpenPin( ControllerPin.V1INTA );
			intPin.SetDriveMode( GpioPinDriveMode.InputPullUp );

			GpioExpander = new Mcp23017( mcpDevice, interruptPin: intPin );
			#endregion

			#region Audio Switch ADG888 set to audio output from FM tuner TEF6686 module
			audioSwitchPin = GpioExpander.OpenPin( ExpanderPin.AudioSwitch );
			audioSwitchPin.SetDriveMode( GpioPinDriveMode.Output );
			audioSwitchPin.Write( GpioPinValue.High );
			#endregion

			#region SN74LVC8T245 level shifter output enable pins
			GpioPin v2V3OutputEnable = GpioExpander.OpenPin( ExpanderPin.V2V3OutputEnable );
			v2V3OutputEnable.SetDriveMode( GpioPinDriveMode.Output );
			v2V3OutputEnable.Write( GpioPinValue.Low );
			#endregion


			_ = new Storage();

			#region Initialize Audio Codec VS1053
			audioCodec = new Vs1053(
				spiControllerName: FEZFeather.SpiBus.Spi6,
				cmdCsPinID: ControllerPin.CodecCS,
				datCsPinID: ControllerPin.CodecDCS,
				dreqPinID: ControllerPin.CodecDREQ,
				resetPinID: ControllerPin.CodecReset );

			audioCodec.Initialize();
			audioCodec.ConfigureStreamingTuning( chunkSize: 32, dataSpiFrequency: 5_000_000 );
			Vs1053.DreqPollIntervalMilliseconds = 1;
			audioCodec.SetVolume( leftChannel: 10, rightChannel: 10 );

			audioControlThreadRunning = true;
			audioControlThread = new Thread( AudioControlWorker );
			audioControlThread.Start();
			#endregion

		}

		/// <summary>
		/// Requests an audio source toggle.
		/// </summary>
		/// <remarks>The request is queued only when the audio control thread is running and no toggle request is
		/// already pending. A true return value does not indicate that the toggle has completed.</remarks>
		/// <returns>true if the request was queued; otherwise, false.</returns>
		private bool RequestAudioSourceToggle()
		{
			if( !audioControlThreadRunning )
			{
				return false;
			}

			lock( audioToggleSync )
			{
				if( audioTogglePending )
				{
					return false;
				}

				audioTogglePending = true;
			}

			audioToggleRequest.Set();
			return true;
		}

		/// <summary>
		/// Waits for audio toggle requests and toggles the audio source.
		/// </summary>
		/// <remarks>Exceptions raised while toggling are written to the debug output; the worker continues
		/// processing.</remarks>
		private void AudioControlWorker()
		{
			while( audioControlThreadRunning )
			{
				audioToggleRequest.WaitOne();

				lock( audioToggleSync )
				{
					audioTogglePending = false;
				}

				try
				{
					ToggleAudioSource();
				}
				catch( Exception ex )
				{
					Debug.WriteLine( $"Audio toggle worker error: {ex.Message}" );
				}
			}
		}

		/// <summary>
		/// Switches audio playback between the FM receiver and the web radio.
		/// </summary>
		/// <remarks>Restores FM audio if web radio playback fails to start or after playback stops. Exceptions are
		/// propagated.</remarks>
		private void ToggleAudioSource()
		{
			if( !isWebRadioActive )
			{
				lock( infoThreadSync )
				{
					infoThreadPaused = true;
					infoThreadResume.Reset();
				}

				rdsSubSystem.Stop();
				while( rdsSubSystem.TryDequeueRdsFrame( out Tef668x.RdsFrame _ ) )
				{
				}

				isWebRadioActive = true;
				vfd.SetLines( WebRadio.TestStationName, WebRadio.TestStationFormat );
				audioSwitchPin.Write( GpioPinValue.Low );

				tef668x?.SetAudioMute( mute: true );
				try
				{
					webRadio.StartPlayback( audioCodec );
				}
				catch
				{
					RestoreFmAudio();
					throw;
				}
				return;
			}

			try
			{
				audioCodec.StopBufferedStream();
			}
			finally
			{
				RestoreFmAudio();
			}
		}

		private void RestoreFmAudio()
		{
			audioSwitchPin.Write( GpioPinValue.High );
			tef668x?.SetAudioMute( mute: false );
			isWebRadioActive = false;
			rdsSubSystem.Start();

			lock( infoThreadSync )
			{
				infoThreadPaused = false;
				infoThreadResume.Set();
			}

			Debug.WriteLine( "Band: switched to FM tuner" );
		}

		// R17
		// R18
		// R15
		// R16

		private void InfoThread()
		{
			const int toggleIntervalMilliseconds = 3000;
			const int loopDelayMilliseconds = 200;
			DateTime nextToggleUtc = DateTime.UtcNow.AddMilliseconds( toggleIntervalMilliseconds );
			DateTime lastRdsUpdateUtc = DateTime.MinValue;
			bool showFrequencyAndSignal = true;
			string rdsLine1 = string.Empty;
			string rdsLine2 = string.Empty;
			ushort lastShownFrequency = tuneFrequency;

			while( infoThreadRunning )
			{
				infoThreadResume.WaitOne();

				lock( infoThreadSync )
				{
					if( infoThreadPaused )
					{
						continue;
					}

					if( tuneFrequency != lastShownFrequency )
					{
						lastShownFrequency = tuneFrequency;
						rdsLine1 = string.Empty;
						rdsLine2 = string.Empty;
						lastRdsUpdateUtc = DateTime.MinValue;
						showFrequencyAndSignal = true;
						nextToggleUtc = DateTime.UtcNow.AddMilliseconds( toggleIntervalMilliseconds );
					}

					bool hasNewFrames = false;

					if( rdsSubSystem != null )
					{
						while( rdsSubSystem.TryDequeueRdsFrame( out Tef668x.RdsFrame _ ) )
						{
							hasNewFrames = true;
						}

						if( hasNewFrames )
						{
							Tef668x.RdsDisplayModel model = rdsSubSystem.GetDisplayModel();

							rdsLine1 = $"{model.PsName} {model.PtyText}".Trim();
							rdsLine2 = model.RadioText ?? string.Empty;
							lastRdsUpdateUtc = DateTime.UtcNow;
						}

						int signalLevel = GetSignalLevel();

						bool hasRdsInfo =
							( !IsNullOrWhiteSpace( rdsLine1 ) || !IsNullOrWhiteSpace( rdsLine2 ) )
							&& ( DateTime.UtcNow - lastRdsUpdateUtc ).TotalSeconds <= 12;

						if( hasRdsInfo )
						{
							if( DateTime.UtcNow >= nextToggleUtc )
							{
								showFrequencyAndSignal = !showFrequencyAndSignal;
								nextToggleUtc = DateTime.UtcNow.AddMilliseconds( toggleIntervalMilliseconds );
							}

							if( showFrequencyAndSignal )
								vfd.SetLines( BuildFrequencyAndSignalLine( signalLevel ), rdsLine2 );
							else
								vfd.SetLines( rdsLine1, rdsLine2 );
						}
						else
						{
							rdsLine1 = string.Empty;
							rdsLine2 = string.Empty;
							showFrequencyAndSignal = true;
							nextToggleUtc = DateTime.UtcNow.AddMilliseconds( toggleIntervalMilliseconds );

							vfd.SetLines( BuildFrequencyAndSignalLine( signalLevel ), string.Empty );
						}

						UpdateIndicatorLeds( hasRdsInfo );
					}
					else
					{
						int signalLevel = GetSignalLevel();
						vfd.SetLines( BuildFrequencyAndSignalLine( signalLevel ), string.Empty );
						UpdateIndicatorLeds( hasRdsInfo: false );
					}
				}

				Thread.Sleep( loopDelayMilliseconds );
			}
		}

		private string BuildFrequencyAndSignalLine( int signalLevel )
		{
			string frequencyText = $"{tuneFrequency / 100.0:F1} MHz";
			string signalMeter = BuildSignalMeter( signalLevel );
			string line = $"{frequencyText} {signalMeter}";

			return FitToVfdWidth( line, 16 );
		}

		private int GetSignalLevel()
		{
			if( !tef668x.TryGetFmQualityData( out ushort qualityStatus, out short rfLevelDbuV10, out ushort usn, out ushort wam, out short offsetKhz10, out ushort bandwidthKhz10, out ushort modulation ) )
			{
				hasTunedSignal = false;
				hasStereoSignal = false;
				WriteSignalDebug( qualityStatus: 0, signalStatus: 0, rfLevelDbuV10: 0, hasUsableSignal: false, level: 0, note: "quality data read failed" );
				return 0;
			}

			if( !tef668x.TryGetFmSignalStatus( out ushort signalStatus ) )
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

			int level;

			if( score < SignalLevel1Threshold )
			{
				level = 1;
			}
			else if( score < SignalLevel2Threshold )
			{
				level = 2;
			}
			else if( score < SignalLevel3Threshold )
			{
				level = 3;
			}
			else if( score < SignalLevel4Threshold )
			{
				level = 4;
			}
			else if( score < SignalLevel5Threshold )
			{
				level = 5;
			}
			else
			{
				level = 6;
			}

			WriteSignalDebug( qualityStatus, signalStatus, rfLevelDbuV10, hasUsableSignal: true, level: level, note: $"score={score} usn={usn} wam={wam} off={offsetKhz10} bw={bandwidthKhz10} mod={modulation}" );
			return level;
		}

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

			Debug.WriteLineIf( EnableDebugOutput, $"SignalDbg f={tuneFrequency} qs=0x{qualityStatus:X4} ts={qualityTimeStamp} rf={rfLevelDbuV10} s=0x{signalStatus:X4} valid={( hasUsableSignal ? 1 : 0 )} lvl={level} {note}" );
		}

		private void UpdateIndicatorLeds( bool hasRdsInfo )
		{
			SetIndicator( ledTunedPin, hasTunedSignal );
			SetIndicator( ledStereoPin, hasTunedSignal && hasStereoSignal );
			SetIndicator( ledRdsPin, hasRdsInfo );
		}

		private static void SetIndicator( GpioPin pin, bool isOn )
		{
			pin.Write( isOn ? GpioPinValue.Low : GpioPinValue.High );
		}

		private static string BuildSignalMeter( int level )
		{
			if( level < 0 )
			{
				level = 0;
			}
			else if( level > 6 )
			{
				level = 6;
			}

			char[] meter = new char[ 6 ];

			for( int i = 0; i < meter.Length; i++ )
			{
				meter[ i ] = i < level ? (char)( 136 + i ) : ' ';
			}

			return new string( meter );
		}

		private static bool IsNullOrWhiteSpace( string value )
		{
			if( value == null || value.Length == 0 )
			{
				return true;
			}

			for( int i = 0; i < value.Length; i++ )
			{
				if( value[ i ] > ' ' )
				{
					return false;
				}
			}

			return true;
		}

		private static string FitToVfdWidth( string value, int width )
		{
			if( value == null )
			{
				value = string.Empty;
			}

			if( value.Length >= width )
			{
				return value.Substring( 0, width );
			}

			char[] text = new char[ width ];

			for( int i = 0; i < width; i++ )
			{
				text[ i ] = i < value.Length ? value[ i ] : ' ';
			}

			return new string( text );
		}

		/// <summary>
		/// Checks whether an I2C device responds at the specified 7-bit address.
		/// </summary>
		/// <remarks>Returns <see langword="false"/> if the I2C controller is not initialized or if the probe
		/// operation throws an exception.</remarks>
		/// <param name="address">7-bit I2C slave address to probe.</param>
		/// <returns><see langword="true"/> when the address is acknowledged; otherwise, <see langword="false"/>.</returns>
		private bool ProbeAddress( byte address )
		{
			bool result = false;

			if( i2cController == null )
			{
				Debug.WriteLine( "I2C controller not initialized!" );
				return false;
			}

			I2cDevice dev =
				i2cController.GetDevice(
					new I2cConnectionSettings( address, I2cMode.Master, I2cAddressFormat.SevenBit, 100_000U ) );

			byte[] rBuf = new byte[ 1 ];

			try
			{
				if( dev.ReadPartial( rBuf ).Status != I2cTransferStatus.SlaveAddressNotAcknowledged )
				{
					result = true;
				}
			}
			catch( Exception ex )
			{
				Debug.WriteLine( $"I2C probe failed for address 0x{address:X2}: {ex.Message}" );
				result = false;
			}

			dev.Dispose();

			return result;
		}

		private bool ApplyTune( bool up, string logMessage )
		{
			Debug.WriteLine( logMessage );

			tuneFrequency += ( ushort )( up ? 10 : -10 );

			tuneFrequency = ( tuneFrequency ) switch
			{
				( <  8700 ) =>  8700,
				( > 10800 ) => 10800,
				_ => tuneFrequency
			};

			tef668x.TuneToFm( tuneFrequency );

			return true;
		}

		private static I2cDevice CreateI2cDevice( I2cController controller, I2cConnectionSettings settings )
		{
			var type = controller.GetType();
			var method =
				type.GetMethod( "CreateI2cDevice", new[] { typeof( I2cConnectionSettings ) } ) ??
				type.GetMethod( "GetDevice", new[] { typeof( I2cConnectionSettings ) } );

			if( method == null )
			{
				throw new NotSupportedException( "Keine kompatible Methode zum Erzeugen eines I2C-Geräts gefunden." );
			}

			return ( I2cDevice )method.Invoke( controller, new object[] { settings } );
		}
	}
}