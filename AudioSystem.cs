using System;
using System.Diagnostics;
using System.Threading;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Gpio;
using ImplicateX.TinyCLR.Drivers.Expander;
using ImplicateX.TinyCLR.Drivers.Decoder;

namespace THERadioReceiver
{
	/// <summary>
	/// Coordinates the VS1053 audio codec and switches playback between the FM receiver
	/// and web radio.
	/// </summary>
	/// <param name="chassis">Provides the board hardware and FM and web radio services.</param>
	public sealed class AudioSystem( Chassis chassis )
	{
		private GpioController gpioController = null!;
		private Mcp23017 gpioExpander = null!;
		public Vs1053 StreamPlayer { get; private set; } = null!;
		private GpioPin SwitchPin { get; set; } = null!;
		private readonly AutoResetEvent audioToggleRequest = new( false );
		private readonly object audioToggleSync = new();
		private Thread audioControlThread = null!;
		private bool audioControlThreadRunning = false;

		private bool audioTogglePending = false;
		private bool isWebRadioActive = false;

		/// <summary>
		/// Initializes the audio switch, codec, and background audio control worker.
		/// </summary>
		/// <remarks>
		/// Call this before requesting an audio source toggle. The codec is configured
		/// for streaming and its volume is set before the worker starts.
		/// </remarks>
		public void Initialize()
		{
			gpioController = chassis.Board.GpioController;
			gpioExpander = chassis.Board.GpioExpander;

			SwitchPin = gpioExpander.OpenPin( Board.ExpanderPin.AudioSwitch );
			SwitchPin.SetDriveMode( GpioPinDriveMode.Output );
			SwitchPin.Write( GpioPinValue.High );

			#region Initialize Audio Codec VS1053
			StreamPlayer = new Vs1053(
				spiControllerName: FEZFeather.SpiBus.Spi6,
				cmdCsPinID: Board.ControllerPin.CodecCS,
				datCsPinID: Board.ControllerPin.CodecDCS,
				dreqPinID: Board.ControllerPin.CodecDREQ,
				resetPinID: Board.ControllerPin.CodecReset );

			StreamPlayer.Initialize();
			StreamPlayer.ConfigureStreamingTuning( chunkSize: 32, dataSpiFrequency: 5_000_000 );

			Vs1053.DreqPollIntervalMilliseconds = 1;

			StreamPlayer.SetVolume( leftChannel: 10, rightChannel: 10 );

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
		public bool RequestAudioSourceToggle()
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
				chassis.FMRadio.PauseForExternalAudio();
				isWebRadioActive = true;

				chassis.Panel.VFDisplay.SetLines( WebRadio.TestStationName, WebRadio.TestStationFormat );

				SwitchPin.Write( GpioPinValue.Low );

				chassis.FMRadio.SetAudioMute( mute: true );

				try
				{
					chassis.WebRadio.StartPlayback( StreamPlayer );
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
				StreamPlayer.StopBufferedStream();
			}
			finally
			{
				RestoreFmAudio();
			}
		}

		/// <summary>
		/// Restores audio output to the FM receiver.
		/// </summary>
		/// <remarks>
		/// Routes audio back to the FM receiver, unmutes it, and resumes FM playback.
		/// </remarks>
		private void RestoreFmAudio()
		{
			SwitchPin.Write( GpioPinValue.High );

			chassis.FMRadio.SetAudioMute( mute: false );
			isWebRadioActive = false;

			chassis.FMRadio.ResumeAfterExternalAudio();

			Debug.WriteLine( "Band: switched to FM tuner" );
		}
	}
}
