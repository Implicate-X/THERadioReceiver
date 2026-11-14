using System;
using ImplicateX.TinyCLR.Drivers.Tuner;
using ImplicateX.TinyCLR.Drivers.Display;

namespace THERadioReceiver
{
	/// <summary>
	/// Updates a 16-character, two-line display with FM tuning information and RDS data.
	/// </summary>
	/// <param name="display">The display to update.</param>
	/// <param name="rdsSubSystem">The tuner subsystem that supplies RDS frames and display data.</param>
	public sealed class FmRadioDisplay( Generic16x2VFD display, Tef668x.RdsSubSystem rdsSubSystem )
	{
		/// <summary>
		/// The interval, in milliseconds, between toggle operations.
		/// </summary>
		private const int ToggleIntervalMilliseconds = 3000;
		private string rdsLine1 = string.Empty;
		private string rdsLine2 = string.Empty;
		/// <summary>
		/// The UTC date and time of the last RDS update.
		/// </summary>
		/// <remarks>Initialized to <see cref="DateTime.MinValue"/> until an update occurs.</remarks>
		private DateTime lastRdsUpdateUtc = DateTime.MinValue;
		/// <summary>
		/// The UTC time at which the next toggle is scheduled.
		/// </summary>
		private DateTime nextToggleUtc = DateTime.UtcNow.AddMilliseconds( ToggleIntervalMilliseconds );
		private bool showFrequencyAndSignal = true;
		private ushort lastShownFrequency;

		/// <summary>
		/// Processes queued RDS frames and refreshes the display.
		/// </summary>
		/// <param name="frequency">The tuned frequency in 10 kHz units.</param>
		/// <param name="signalLevel">The signal strength, clamped to the meter's 0–6 range.</param>
		/// <returns><see langword="true"/> if non-empty RDS information was updated within the last 12 seconds; otherwise, <see langword="false"/>.</returns>
		/// <remarks>
		/// When recent RDS information is available, the first display line alternates
		/// between frequency and signal information and the station name and program type.
		/// Radio text is shown on the second line.
		/// </remarks>
		public bool Update( ushort frequency, int signalLevel )
		{
			if( frequency != lastShownFrequency )
			{
				lastShownFrequency = frequency;
				ClearRdsInformation();
			}

			bool hasNewFrames = false;

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

			bool hasRdsInfo =
				( !IsNullOrWhiteSpace( rdsLine1 ) || !IsNullOrWhiteSpace( rdsLine2 ) )
				&& ( DateTime.UtcNow - lastRdsUpdateUtc ).TotalSeconds <= 12;

			if( hasRdsInfo )
			{
				if( DateTime.UtcNow >= nextToggleUtc )
				{
					showFrequencyAndSignal = !showFrequencyAndSignal;
					nextToggleUtc = DateTime.UtcNow.AddMilliseconds( ToggleIntervalMilliseconds );
				}

				if( showFrequencyAndSignal )
					display.SetLines( BuildFrequencyAndSignalLine( frequency, signalLevel ), rdsLine2 );
				else
					display.SetLines( rdsLine1, rdsLine2 );
			}
			else
			{
				ClearRdsInformation();
				display.SetLines( BuildFrequencyAndSignalLine( frequency, signalLevel ), string.Empty );
			}

			return hasRdsInfo;
		}

		/// <summary>
		/// Discards queued RDS frames and clears the cached RDS information.
		/// </summary>
		/// <remarks>The display is refreshed on the next call to <see cref="Update"/>.</remarks>
		public void Clear()
		{
			while( rdsSubSystem.TryDequeueRdsFrame( out Tef668x.RdsFrame _ ) )
			{
			}

			ClearRdsInformation();
		}

		/// <summary>
		/// Clears the RDS information and resets the display state.
		/// </summary>
		private void ClearRdsInformation()
		{
			rdsLine1 = string.Empty;
			rdsLine2 = string.Empty;
			lastRdsUpdateUtc = DateTime.MinValue;
			showFrequencyAndSignal = true;
			nextToggleUtc = DateTime.UtcNow.AddMilliseconds( ToggleIntervalMilliseconds );
		}

		/// <summary>
		/// Formats a frequency and signal meter for a 16-character VFD line.
		/// </summary>
		/// <param name="frequency">The frequency in hundredths of a megahertz.</param>
		/// <param name="signalLevel">The signal level used to build the signal meter.</param>
		/// <returns>A frequency and signal-meter line fitted to 16 characters.</returns>
		private static string BuildFrequencyAndSignalLine( ushort frequency, int signalLevel )
		{
			string frequencyText = $"{frequency / 100.0:F1} MHz";
			string signalMeter = BuildSignalMeter( signalLevel );
			return FitToVfdWidth( $"{frequencyText} {signalMeter}", 16 );
		}

		/// <summary>
		/// Creates a six-character signal meter for the specified level.
		/// </summary>
		/// <param name="level">The signal level, clamped to the range 0 through 6.</param>
		/// <returns>A six-character string with the first <paramref name="level"/> positions filled and the remaining positions blank.</returns>
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
				meter[ i ] = i < level ? ( char )( 136 + i ) : ' ';
			}

			return new string( meter );
		}

		/// <summary>
		/// Determines whether a string is null, empty, or contains only characters at or below the space character.
		/// </summary>
		/// <remarks>Characters above the space character, including some Unicode whitespace characters, are not
		/// treated as whitespace.</remarks>
		/// <param name="value">The string to test.</param>
		/// <returns>true if value is null, empty, or contains only characters at or below the space character; otherwise, false.</returns>
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
		
		/// <summary>
		/// Fits a string to the specified VFD width by truncating or padding with spaces.
		/// </summary>
		/// <param name="value">The string to fit.</param>
		/// <param name="width">The desired width.</param>
		/// <returns>A string fitted to the specified width.</returns>
		private static string FitToVfdWidth( string value, int width )
		{
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
	}
}