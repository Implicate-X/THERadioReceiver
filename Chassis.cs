using System;

namespace THERadioReceiver
{
	/// <summary>
	/// Creates and initializes the components that make up the radio receiver chassis.
	/// </summary>
	public sealed class Chassis : IDisposable
	{
		/// <summary>
		/// Gets the receiver's main board.
		/// </summary>
		public Board Board { get; private set; } = null!;

		/// <summary>
		/// Gets the receiver's control front panel.
		/// </summary>
		public Panel Panel { get; private set; } = null!;

		/// <summary>
		/// Gets the FM radio component.
		/// </summary>
		public FmRadio FMRadio { get; private set; } = null!;

		/// <summary>
		/// Gets the web radio component.
		/// </summary>
		public WebRadio WebRadio { get; private set; } = null!;

		/// <summary>
		/// Gets the audio system component.
		/// </summary>
		public AudioSystem AudioSystem { get; private set; } = null!;

		/// <summary>
		/// Gets the Ethernet interface associated with the chassis.
		/// </summary>
		public Ethernet Ethernet { get; private set; } = null!;

		/// <summary>
		/// Gets the settings for this instance.
		/// </summary>
		public Settings Settings { get; private set; } = null!;

		/// <summary>
		/// Gets the storage associated with the chassis.
		/// </summary>
		public Storage Storage { get; private set; } = null!;

		/// <summary>Creates the chassis using the configured physical storage locations.</summary>
		public Chassis() : this( null! )
		{
		}

		/// <summary>Creates the chassis with independent, injectable storage locations.</summary>
		public Chassis( Storage storage )
		{
			Storage = storage;
			Board = new();
			Panel = new( this );
			FMRadio = new( this );
			WebRadio = new( this );
			AudioSystem = new( this );
			//Ethernet = new
			//( 
			//	chassis: this, 
			//	chipSelectPinId: Board.ControllerPin.EthernetCS, 
			//	interruptPinId: Board.ControllerPin.EthernetINT, 
			//	resetPinId: Board.ControllerPin.EthernetReset, 
			//	ethernetNetworkInterfaceSettings: new EthernetNetworkInterfaceSettings
			//{
			//	// TODO: Change the following settings to match your network configuration.
			//	Address = new IPAddress( [ 192, 168, 1, 53 ] ),
			//	SubnetMask = new IPAddress( [ 255, 255, 255, 0 ] ),
			//	GatewayAddress = new IPAddress( [ 192, 168, 1, 1 ] ),
			//	DnsAddresses = [ new( [ 192, 168, 1, 1 ] ), new( [ 192, 168, 1, 254 ] ), ],
			//	MacAddress = [ 0x00, 0x04, 0x00, 0x00, 0x00, 0x00 ],
			//	DhcpEnable = false,
			//	DynamicDnsEnable = false
			//} );

			Initialize();
		}

		/// <summary>
		/// Initializes each chassis component.
		/// </summary>
		private void Initialize()
		{
			Board.Initialize();
			Storage ??= Storage.CreateDefault( Board.I2cController );
			Settings = new Settings( new Persistence( Storage.North, new BsonSerializer() ) );
			Settings.Load();
			Panel.Initialize();
			FMRadio.Initialize();
			WebRadio.Initialize();
			AudioSystem.Initialize();
			//Ethernet.Initialize();
		}

		public void Dispose()
		{
			Storage.Dispose();
		}
	}
}