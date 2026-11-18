using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using GHIElectronics.TinyCLR.Data.Json;
using GHIElectronics.TinyCLR.Devices.Network;

namespace THERadioReceiver
{
	/// <summary>
	/// Loads <see cref="Settings"/> data from JSON.
	/// </summary>
	public static class JsonHelper
	{
		/// <summary>
		/// Deserializes the specified JSON text into a <see cref="Settings"/> instance.
		/// </summary>
		/// <param name="json">The JSON text to deserialize.</param>
		/// <returns>The deserialized settings.</returns>
		public static Settings Load( string json ) => ( Settings )JsonConverter.DeserializeObject( json, typeof( Settings ) );
	}

	[Serializable]
	/// <summary>
	/// Holds the radio receiver settings used for JSON serialization and deserialization.
	/// </summary>
	public sealed class Settings
	{
		public EthernetNetworkInterfaceSettings EthernetSettings { get; private set; } = null!;

		public void Load()
		{
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

			try
			{
				JToken jTokenIn = JsonConverter.Serialize( this, new JsonSerializerSettings
				{
					TypeNameHandling = TypeNameHandling.Auto
				} );

				Debug.WriteLine( $"Serial Input:\n{jTokenIn.ToString()}" );

				JToken jTokenOut = JsonConverter.Deserialize( jTokenIn.ToString() );

				Debug.WriteLine( $"Serial Output:\n{jTokenOut.ToString()}" );

				var settings = JsonHelper.Load( jTokenOut.ToString() );

				if( settings.EthernetSettings == null )
					throw new Exception( "Deserialized settings do not contain EthernetSettings." );

				Debug.WriteLine( $"Deserialized settings:\n{JsonConverter.Serialize( settings )}" );
			}
			catch( Exception ex )
			{
				Debug.WriteLine( "Serialization error: " + ex.Message );
			}
		}

		public void Save()
		{
		}
	}
}
