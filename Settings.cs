using System;
using System.Diagnostics;
using System.Net;
using GHIElectronics.TinyCLR.Devices.Network;

namespace THERadioReceiver
{
	// <summary>Holds the radio receiver settings.</summary>
	/// <summary>
	/// Stores and loads radio receiver settings.
	/// </summary>
	/// <remarks>If Ethernet settings cannot be loaded, default network settings are used.</remarks>
	/// <param name="persistence">The persistence provider used to load and save settings.</param>
	public sealed class Settings( Persistence persistence )
	{
		/// <summary>
		/// The persistence component used by this type.
		/// </summary>
		private readonly Persistence persistence = persistence ?? throw new ArgumentNullException( nameof( persistence ) );

		/// <summary>
		/// Gets the Ethernet network interface settings.
		/// </summary>
		public EthernetNetworkInterfaceSettings EthernetSettings { get; private set; } = null!;

		/// <summary>
		/// Loads Ethernet settings from persistence, or assigns default settings if loading fails or no saved settings are
		/// found.
		/// </summary>
		public void Load()
		{
			try
			{
				if( persistence.TryLoad( nameof( EthernetSettings ), typeof( EthernetNetworkInterfaceSettings ), out object value ) )
				{
					EthernetSettings = ( EthernetNetworkInterfaceSettings )value;
					return;
				}
				Debug.WriteLine( "Ethernet settings not found; using defaults." );
			}
			catch( Exception ex )
			{
				Debug.WriteLine( "Ethernet settings could not be loaded; using defaults: " + ex.Message );
			}

			EthernetSettings = new EthernetNetworkInterfaceSettings
			{
				Address = new IPAddress( [ 192, 168, 1, 53 ] ),
				SubnetMask = new IPAddress( [ 255, 255, 255, 0 ] ),
				GatewayAddress = new IPAddress( [ 192, 168, 1, 1 ] ),
				DnsAddresses = [ new( [ 192, 168, 1, 1 ] ), new( [ 192, 168, 1, 254 ] ), ],
				MacAddress = [ 0x00, 0x04, 0x00, 0x00, 0x00, 0x00 ],
				DhcpEnable = false,
				DynamicDnsEnable = false
			};
		}

		/// <summary>
		/// Saves the Ethernet settings.
		/// </summary>
		public void Save()
		{
			persistence.Save( nameof( EthernetSettings ), EthernetSettings );
		}
	}
}
