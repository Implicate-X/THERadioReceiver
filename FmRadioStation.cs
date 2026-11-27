using System;
using System.Diagnostics;

namespace THERadioReceiver
{
	public sealed partial class FmRadio
	{
		/// <summary>
		/// Represents an FM radio station.
		/// </summary>
		/// <remarks>The station frequency is expressed in 10 kHz increments.</remarks>
		public sealed class Station
		{
			public string Name { get; set; } = string.Empty;
			public ushort Frequency10kHz { get; set; }
		}

		/// <summary>
		/// Provides the FM station list and loads or saves it through persistence.
		/// </summary>
		/// <remarks>If the saved station list is unavailable or invalid, the default station list is used.</remarks>
		/// <param name="persistence">The persistence provider used to load and save the station list.</param>
		public sealed class Stations( Persistence persistence )
		{
			private readonly Persistence persistence = persistence ?? throw new ArgumentNullException( nameof( persistence ) );
			public Station[] Items { get; private set; } = Defaults();

			public void Load()
			{
				try
				{
					if( persistence.TryLoad( "Stations", typeof( Station[] ), out object value ) )
					{
						Station[] stations = ( Station[] )value;

						if( stations.Length == 0 )
							throw new FormatException( "FM station list is empty." );
						
						foreach( Station station in stations )
							if( station == null || station.Frequency10kHz < 8700 || station.Frequency10kHz > 10800 ) 
								throw new FormatException( "Invalid FM station." );
						
						Items = stations;

						return;
					}
					Debug.WriteLine( "FM stations not found; using defaults." );
				}
				catch( Exception ex ) 
				{
					Debug.WriteLine( "FM stations could not be loaded; using defaults: " + ex.Message );
				}

				Items = Defaults();
			}

			/// <summary>
			/// Saves the station items.
			/// </summary>
			public void Save() => persistence.Save( "Stations", Items );

			/// <summary>
			/// Gets the default FM station configuration.
			/// </summary>
			/// <returns>An array containing the default FM station, WDR 2 at 99.2 MHz.</returns>
			private static Station[] Defaults() => [new Station { Name = "WDR 2", Frequency10kHz = 9920 }];
		}
	}
}
