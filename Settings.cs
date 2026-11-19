using System;
using System.Diagnostics;
using System.Net;
using GHIElectronics.TinyCLR.Devices.Network;

namespace THERadioReceiver
{
	/// <summary>Holds the radio receiver settings.</summary>
	public sealed class Settings( Persistence persistence )
	{
		private readonly Persistence persistence = persistence ?? throw new ArgumentNullException( nameof( persistence ) );

		public EthernetNetworkInterfaceSettings EthernetSettings { get; private set; } = null!;

		public void Load()
		{
			if( persistence.TryLoad( nameof( EthernetSettings ), typeof( EthernetNetworkInterfaceSettings ), out object value ) )
			{
				EthernetSettings = ( EthernetNetworkInterfaceSettings )value;
				return;
			}

			Debug.WriteLine( "Ethernet settings not found; using defaults." );

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

		public void Save()
		{
			persistence.Save( nameof( EthernetSettings ), EthernetSettings );
		}
	}
}
