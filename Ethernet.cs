using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Diagnostics;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Spi;
using GHIElectronics.TinyCLR.Devices.Network;

namespace THERadioReceiver
{
	public sealed partial class Ethernet( 
		Chassis chassis,
		int chipSelectPinId,
		int interruptPinId,
		int resetPinId,
		EthernetNetworkInterfaceSettings ethernetNetworkInterfaceSettings,
		string spiApiName = SC20100.SpiBus.Spi4,
		string spiGpioApiName = SC20100.GpioPin.Id,
		string networkControllerName = SC20100.NetworkController.Enc28j60
	)
	{
		public Chassis Chassis { get; private set; } = chassis;

		bool linkReady = false;

		public void Initialize()
		{
			//gpioController = GpioController.GetDefault();

			//var networkController = NetworkController.FromName( networkControllerName );

			//GpioPin chipSelectPin = gpioController.OpenPin( chipSelectPinId );

			//var spiSettings = new SpiConnectionSettings
			//{
			//	ChipSelectLine = chipSelectPin,
			//	ClockFrequency = 4_000_000,
			//	Mode = SpiMode.Mode0,
			//	ChipSelectType = SpiChipSelectType.Gpio,
			//	ChipSelectHoldTime = TimeSpan.FromTicks( 10 ),
			//	ChipSelectSetupTime = TimeSpan.FromTicks( 10 ),
			//};

			//var networkCommunicationInterfaceSettings = new SpiNetworkCommunicationInterfaceSettings
			//{
			//	SpiApiName = spiApiName,
			//	GpioApiName = spiGpioApiName,
			//	SpiSettings = spiSettings,
			//	InterruptPin = gpioController.OpenPin( interruptPinId ),
			//	InterruptEdge = GpioPinEdge.FallingEdge,
			//	InterruptDriveMode = GpioPinDriveMode.InputPullUp,
			//	ResetPin = gpioController.OpenPin( resetPinId ),
			//	ResetActiveState = GpioPinValue.Low,
			//};

			//var networkInterfaceSetting = new EthernetNetworkInterfaceSettings
			//{
			//	// TODO: Change the following settings to match your network configuration.
			//	Address = new IPAddress( [ 192, 168, 1, 53 ] ),
			//	SubnetMask = new IPAddress( [ 255, 255, 255, 0 ] ),
			//	GatewayAddress = new IPAddress( [ 192, 168, 1, 1 ] ),
			//	DnsAddresses = [ new( [ 192, 168, 1, 1 ] ), new( [ 192, 168, 1, 254 ] ), ],
			//	MacAddress = [ 0x00, 0x04, 0x00, 0x00, 0x00, 0x00 ],
			//	DhcpEnable = false,
			//	DynamicDnsEnable = false
			//};

			//networkController.SetInterfaceSettings( ethernetNetworkInterfaceSettings );
			//networkController.SetCommunicationInterfaceSettings( networkCommunicationInterfaceSettings );
			//networkController.SetAsDefaultController();

			//networkController.NetworkAddressChanged += NetworkController_NetworkAddressChanged; ;
			//networkController.NetworkLinkConnectedChanged += NetworkController_NetworkLinkConnectedChanged; ;

			//networkController.Enable();

			//while( !linkReady )
			//{
			//	Thread.Sleep( 10 );
			//}

			//Debug.WriteLine( "Network is ready to use" );
		}

		private void NetworkController_NetworkLinkConnectedChanged( NetworkController sender, NetworkLinkConnectedChangedEventArgs e )
		{
			linkReady = e.Connected;
			Debug.WriteLine( "Network link connected changed: " + e.Connected );

		}

		private void NetworkController_NetworkAddressChanged( NetworkController sender, NetworkAddressChangedEventArgs e )
		{
			Debug.WriteLine( "Network address changed: " + e.Timestamp.ToString() );
		}
	}
}