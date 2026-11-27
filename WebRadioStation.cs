using System;
using System.Diagnostics;

namespace THERadioReceiver
{
	public sealed partial class WebRadio
	{
		/// <summary>
		/// Represents a web radio station.
		/// </summary>
		public sealed class Station
		{
			public string Name { get; set; } = string.Empty;
			public string Format { get; set; } = string.Empty;
			public string Url { get; set; } = string.Empty;
		}

		/// <summary>
		/// Loads and saves the configured web stations.
		/// </summary>
		/// <remarks>If saved stations are missing or invalid, the default station list is used.</remarks>
		/// <param name="persistence">The persistence service used to load and save web stations.</param>
		public sealed class Stations( Persistence persistence )
		{
			/// <summary>
			/// Stores the persistence dependency.
			/// </summary>
			private readonly Persistence persistence = persistence ?? throw new ArgumentNullException( nameof( persistence ) );
			public Station[] Items { get; private set; } = Defaults();

			/// <summary>
			/// Loads persisted web stations, using the default stations if none are available or the saved data is invalid.
			/// </summary>
			public void Load()
			{
				try
				{
					if( persistence.TryLoad( "Stations", typeof( Station[] ), out object value ) )
					{
						Station[] stations = ( Station[] )value;

						if( stations.Length == 0 ) 
							throw new FormatException( "Web station list is empty." );
						
						foreach( Station station in stations )
							if( station == null || 
								string.IsNullOrEmpty( station.Name ) || 
								string.IsNullOrEmpty( station.Url ) || 
								( station.Url.IndexOf( "http://" ) != 0 && 
								station.Url.IndexOf( "https://" ) != 0 ) )
								throw new FormatException( "Invalid web station." );

						Items = stations;

						return;
					}
					Debug.WriteLine( "Web stations not found; using defaults." );
				}
				catch( Exception ex ) { Debug.WriteLine( "Web stations could not be loaded; using defaults: " + ex.Message ); }
				Items = Defaults();
			}

			/// <summary>
			/// Persists the station items.
			/// </summary>
			public void Save() => persistence.Save( "Stations", Items );

			/// <summary>
			/// Creates the default web station list.
			/// </summary>
			/// <returns>An array containing the default Bremen Zwei web station.</returns>
			private static Station[] Defaults() => 
			[
				new Station 
				{
					Name = "Bremen Zwei", 
					Format = "MP3 128 kbit/s", 
					Url = "http://media-ice.musicradio.com/CapitalMP3"
				}
			];
		}
	}
}
