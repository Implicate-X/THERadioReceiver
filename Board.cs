using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.I2c;
using ImplicateX.TinyCLR.Drivers.Expander;

namespace THERadioReceiver
{
	/// <summary>
	/// Provides hardware pin assignments and initializes the receiver's board interfaces.
	/// </summary>
	public sealed class Board()
	{
		/// <summary>
		/// Contains the 7-bit I²C addresses of devices connected to the receiver's I²C bus.
		/// </summary>
		public static class I2cAddress
		{
			/// <summary>I²C address of the MCP23017 GPIO expander.</summary>
			public const byte Expander = 0x20;

			/// <summary>I²C address of the MCP23017 GPIO OLED expander.</summary>
			public const byte OledExpander = 0x22;

			/// <summary>I²C address of the real-time clock.</summary>
			public const byte RTC = 0x52;

			/// <summary>I²C address of the radio tuner.</summary>
			public const byte Tuner = 0x64;
		}

		/// <summary>
		/// Contains GPIO pin assignments on the FEZ Feather controller.
		/// </summary>
		public static class ControllerPin
		{
			/// <summary>Pin connected to the remote-control input.</summary>
			public const int RemoteCtrl = FEZFeather.GpioPin.PA0; // 5

			/// <summary>Ethernet chip-select pin.</summary>
			public const int EthernetCS = FEZFeather.GpioPin.PA3; // 6

			/// <summary>Audio codec SPI MISO pin.</summary>
			public const int CodecMISO = FEZFeather.GpioPin.PA6; // 7

			/// <summary>Audio codec SPI MOSI pin.</summary>
			public const int CodecMOSI = FEZFeather.GpioPin.PA7; // 8

			/// <summary>Audio codec SPI clock pin.</summary>
			public const int CodecSCLK = FEZFeather.GpioPin.PA5; // 9

			/// <summary>Power-switch input pin.</summary>
			public const int PowerSwitch = FEZFeather.GpioPin.PB1; // 10

			/// <summary>Ethernet SPI clock pin.</summary>
			public const int EthernetSCLK = FEZFeather.GpioPin.PE12; // 11

			/// <summary>Ethernet SPI MOSI pin.</summary>
			public const int EthernetMOSI = FEZFeather.GpioPin.PE14; // 12

			/// <summary>Ethernet SPI MISO pin.</summary>
			public const int EthernetMISO = FEZFeather.GpioPin.PE13; // 13

			/// <summary>Ethernet interrupt input pin.</summary>
			public const int EthernetINT = FEZFeather.GpioPin.PA10; // 14

			/// <summary>Ethernet reset pin.</summary>
			public const int EthernetReset = FEZFeather.GpioPin.PA9; // 15

			/// <summary>Decoder SPI clock pin.</summary>
			public const int DecoderSCLK = FEZFeather.GpioPin.PC8; // 17

			/// <summary>Decoder SPI MOSI pin.</summary>
			public const int DecoderMOSI = FEZFeather.GpioPin.PC9; // 18

			/// <summary>Decoder SPI MISO pin.</summary>
			public const int DecoderMISO = FEZFeather.GpioPin.PC10; // 19

			/// <summary>Decoder chip-select pin.</summary>
			public const int DecoderCS = FEZFeather.GpioPin.PC11; // 20

			/// <summary>Interrupt input from MCP23017 Port A.</summary>
			public const int V1INTA = FEZFeather.GpioPin.PC12; // 21

			/// <summary>Reset pin for the first-level I/O expander.</summary>
			public const int V1Reset = FEZFeather.GpioPin.PD2; // 22

			/// <summary>Tuner RDS signal input pin.</summary>
			public const int TunerRDS = FEZFeather.GpioPin.PE6; // 37

			/// <summary>Audio codec chip-select pin.</summary>
			public const int CodecCS = FEZFeather.GpioPin.PE0; // 36

			/// <summary>Audio codec data/command select pin.</summary>
			public const int CodecDCS = FEZFeather.GpioPin.PE1; // 35

			/// <summary>Vacuum fluorescent display reset pin.</summary>
			public const int VFReset = FEZFeather.GpioPin.PD6; // 34

			/// <summary>Vacuum fluorescent display chip-select pin.</summary>
			public const int VFDCS = FEZFeather.GpioPin.PD5; // 33

			/// <summary>Vacuum fluorescent display data pin.</summary>
			public const int VFDData = FEZFeather.GpioPin.PD4; // 32

			/// <summary>Vacuum fluorescent display clock pin.</summary>
			public const int VFDClock = FEZFeather.GpioPin.PD3; // 31

			/// <summary>Auxiliary I²C bus clock pin.</summary>
			public const int A1SCL = FEZFeather.GpioPin.PB8; // 30

			/// <summary>Auxiliary I²C bus data pin.</summary>
			public const int A1SDA = FEZFeather.GpioPin.PB9; // 29

			/// <summary>Interrupt input from the second-level I/O expander.</summary>
			public const int IOExtINTA = FEZFeather.GpioPin.PD0; // 28

			/// <summary>Reset pin for the second-level I/O expander.</summary>
			public const int IOExtReset = FEZFeather.GpioPin.PD1; // 27

			/// <summary>Audio codec data-request input pin.</summary>
			public const int CodecDREQ = FEZFeather.GpioPin.PE7; // 26

			/// <summary>Audio codec reset pin.</summary>
			public const int CodecReset = FEZFeather.GpioPin.PE8; // 25
		}

		/// <summary>
		/// Contains pin numbers on the MCP23017 GPIO expander.
		/// </summary>
		public static class ExpanderPin
		{
			/// <summary>Menu key input.</summary>
			public const int KeyMenu = 0;

			/// <summary>Autopreset key input.</summary>
			public const int KeyAutopreset = 1;

			/// <summary>Band-selection key input.</summary>
			public const int KeyBand = 2;

			/// <summary>Display-dimmer key input.</summary>
			public const int KeyDimmer = 3;

			/// <summary>Rotary encoder A input.</summary>
			public const int EncoderA = 5;

			/// <summary>Rotary encoder B input.</summary>
			public const int EncoderB = 6;

			/// <summary>Rotary encoder push-button input.</summary>
			public const int EncoderKey = 7;

			/// <summary>Audio codec chip-select control.</summary>
			public const int CodecCCS = 8;

			/// <summary>Output-enable control for the V2/V3 level shifter.</summary>
			public const int V2V3OutputEnable = 9;

			/// <summary>Audio-path switch control.</summary>
			public const int AudioSwitch = 10;

			/// <summary>Tuner reset control.</summary>
			public const int TunerReset = 11;

			/// <summary>Real-time clock event input.</summary>
			public const int RTCEVI = 12;

			/// <summary>RDS indicator LED control.</summary>
			public const int LedRDS = 13;

			/// <summary>Tuned indicator LED control.</summary>
			public const int LedTuned = 14;

			/// <summary>Stereo indicator LED control.</summary>
			public const int LedStereo = 15;
		}

		/// <summary>
		/// Contains pin numbers on the MCP23017 OLED GPIO expander.
		/// </summary>
		public static class OledExpanderPin
		{
			public const int EnableRead = 0;
			public const int ChipSelect = 1;

			public const int DataCommand = 2;

			public const int ReadWrite = 3;

			public const int A1Reset = 4;

			public const int V2Select0 = 5;

			public const int V2Select1 = 6;

			public const int WriteProtect = 7;

			public const int Data0 = 8;

			public const int Data1 = 9;

			public const int Data2 = 10;

			public const int Data3 = 11;

			public const int Data4 = 12;

			public const int Data5 = 13;

			public const int Data6 = 14;

			public const int Data7 = 15;
		}

		/// <summary>Gets the initialized MCP23017 GPIO expander.</summary>
		public Mcp23017 GpioExpander { get; private set; } = null!;

		/// <summary>Gets the initialized MCP23017 GPIO OLED expander.</summary>
		public Mcp23017 GpioOledExpander { get; private set; } = null!;

		/// <summary>Gets the board's GPIO controller.</summary>
		public GpioController GpioController { get; private set; } = null!;

		/// <summary>Gets the board's I²C controller.</summary>
		public I2cController I2cController { get; private set; } = null!;

		/// <summary>
		/// Gets or sets the I²C addresses of the connected devices.
		/// </summary>
		/// <remarks>Initialized with the addresses of the expanders, RTC and tuner.</remarks>
		public List<byte> I2cAdresses { get; set; } = 
		[ 
			I2cAddress.Expander,
			I2cAddress.OledExpander,
			I2cAddress.RTC,
			I2cAddress.Tuner
		];

		/// <summary>
		/// Initializes the GPIO and I²C controllers, verifies the expected I²C devices,
		/// resets and initializes the MCP23017, and configures the V2/V3 level-shifter output.
		/// </summary>
		/// <remarks>
		/// Initialization returns without configuring the expander if any expected I²C device
		/// does not acknowledge its address.
		/// </remarks>
		public void Initialize()
		{
			GpioController = GpioController.GetDefault();
			I2cController = I2cController.FromName( FEZFeather.I2cBus.I2c1 );

			#region I2C bus scan
			if( !I2cAdresses.TrueForAll( ProbeAddress ) )
			{
				Debug.WriteLine( "Warning: Not all expected I2C devices were found on the bus!" );
				return;
			}
			#endregion

			#region IO Expander MCP23017 Reset sequence
			GpioPin v1ResetPin = GpioController.OpenPin( ControllerPin.V1Reset );
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

			#region OLED Expander MCP23017 Reset sequence
			GpioPin ioResetPin = GpioController.OpenPin( ControllerPin.IOExtReset );
			ioResetPin.SetDriveMode( GpioPinDriveMode.Output );
			ioResetPin.Write( GpioPinValue.High );
			Thread.Sleep( 100 );
			ioResetPin.Write( GpioPinValue.Low );
			Thread.Sleep( 100 );
			ioResetPin.Write( GpioPinValue.High );
			Thread.Sleep( 100 );
			#endregion

			#region OLED Expander MCP23017 initialization
			var oledExpander = CreateI2cDevice(
				I2cController,
				new I2cConnectionSettings( I2cAddress.OledExpander, I2cMode.Master, I2cAddressFormat.SevenBit ) );

			GpioOledExpander = new Mcp23017( oledExpander, resetPin: ioResetPin );
			#endregion

			#region SN74LVC8T245 level shifter output enable pins
			GpioPin v2V3OutputEnable = GpioExpander.OpenPin( ExpanderPin.V2V3OutputEnable );
			v2V3OutputEnable.SetDriveMode( GpioPinDriveMode.Output );
			v2V3OutputEnable.Write( GpioPinValue.Low );
			#endregion
		}

		/// <summary>
		/// Checks whether an I²C device responds at the specified 7-bit address.
		/// </summary>
		/// <remarks>
		/// Returns <see langword="false"/> if the controller is not initialized or if the read
		/// operation throws an exception.
		/// </remarks>
		/// <param name="address">The 7-bit I²C slave address to probe.</param>
		/// <returns>
		/// <see langword="true"/> if the address is acknowledged; otherwise, <see langword="false"/>.
		/// </returns>
		private bool ProbeAddress(byte address)
		{
			bool result = false;

			if( I2cController == null )
			{
				Debug.WriteLine( "I2C controller not initialized!" );
				return false;
			}

			I2cDevice dev =
				I2cController.GetDevice(
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

		/// <summary>
		/// Creates an I²C device using the supported creation method exposed by the controller.
		/// </summary>
		/// <param name="controller">The I²C controller that creates the device.</param>
		/// <param name="settings">Connection settings for the device.</param>
		/// <returns>The created I²C device.</returns>
		/// <exception cref="NotSupportedException">
		/// Neither a compatible <c>CreateI2cDevice</c> nor <c>GetDevice</c> method is available.
		/// </exception>
		private static I2cDevice CreateI2cDevice(I2cController controller, I2cConnectionSettings settings)
		{
			var type = controller.GetType();
			var method =
				type.GetMethod("CreateI2cDevice", new[] { typeof(I2cConnectionSettings) }) ??
				type.GetMethod("GetDevice", new[] { typeof(I2cConnectionSettings) });

			if( method == null )
			{
				throw new NotSupportedException("Keine kompatible Methode zum Erzeugen eines I2C-Geräts gefunden.");
			}

			return (I2cDevice)method.Invoke(controller, new object[] { settings });
		}
	}
}