using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Text.RegularExpressions;
using ImplicateX.TinyCLR.Drivers.Display;

namespace THERadioReceiver
{
	// Edits and persists the receiver's three IPv4 network addresses through the front panel and remote control.
	/// <summary>
	/// Edits and saves the receiver's IPv4 address, subnet mask, and gateway settings.
	/// </summary>
	/// <remarks>The editor accepts IPv4 octets from 0 to 255. Changes are saved when the save command is issued;
	/// closing the editor without saving discards them.</remarks>
	/// <param name="settings">The settings containing the receiver's Ethernet configuration.</param>
	/// <param name="display">The 16×2 display used to show the network settings editor.</param>
	/// <param name="editingStarted">Callback invoked when editing starts.</param>
	/// <param name="editingStopped">Callback invoked when editing stops.</param>
	public sealed class NetworkSettingsMenu( Settings settings, Generic16x2VFD display, Action editingStarted, Action editingStopped )
	{
		private static readonly int[] DigitPositions = [ 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14 ];
		private static readonly string[] FieldNames = [ "Device IP", "Subnet mask", "Gateway IP" ];
		private readonly object syncRoot = new();
		private char[][] addresses = null!;
		private int selectedField;
		private int selectedDigit;
		private bool isActive;
		private bool showSelectedDigit = true;
		private bool hasInvalidOctet;

		/// <summary>
		/// Gets whether the network settings editor is active.
		/// </summary>
		public bool IsActive
		{
			get
			{
				lock( syncRoot )
				{
					return isActive;
				}
			}
		}

		/// <summary>
		/// Starts editing the network addresses, or closes the editor without saving if it is already active.
		/// </summary>
		public void HandleMenuKey()
		{
			lock( syncRoot )
			{
				if( isActive )
				{
					isActive = false;
					editingStopped();
					return;
				}

				addresses =
				[
					FormatIPv4Address( settings.EthernetSettings.Address ),
					FormatIPv4Address( settings.EthernetSettings.SubnetMask ),
					FormatIPv4Address( settings.EthernetSettings.GatewayAddress )
				];
				selectedField = 0;
				selectedDigit = 0;
				showSelectedDigit = true;
				hasInvalidOctet = false;
				isActive = true;
			}

			editingStarted();
			new Thread( DisplayLoop ).Start();
		}

		/// <summary>
		/// Processes a remote-control command while the editor is active.
		/// </summary>
		/// <param name="commandName">The name of the remote-control command to process.</param>
		public void HandleRemoteCommand( string commandName )
		{
			lock( syncRoot )
			{
				if( !isActive )
				{
					return;
				}

				Action commandAction = commandName switch
				{
					"Tuner Preset +"	=> () => SelectField( selectedField + 1 ),
					"Tuner Preset -"	=> () => SelectField( selectedField - 1 ),
					"Tuning +"			=> () => MoveCursor( 1 ),
					"Tuning -"			=> () => MoveCursor( -1 ),
					"Tuner Auto/Manu"	=> () => SetDigit( 0 ),
					"Tuner Memory"		=> () => { Save(); return; },
					"Tuner Shift"		=> () => { isActive = false; editingStopped(); return; }
					,_					=> () => SetDigit( Regex.Match( commandName, @"^Tuner Channel ([0-9])$" ) is { Success: true } m ? int.Parse( m.Groups[ 1 ].Value ) : -1 )
				};

				commandAction();

				if( isActive )
				{
					Render( true );
				}
			}
		}

		/// <summary>
		/// Sets the digit at the current cursor position.
		/// </summary>
		/// <param name="digit">The digit to set.</param>
		private void SetDigit( int digit )
		{
			addresses[ selectedField ][ DigitPositions[ selectedDigit ] ] = ( char )( '0' + digit );
			hasInvalidOctet = false;
			MoveCursor( 1 );
		}

		/// <summary>
		/// Moves the selection by the specified number of digit positions and displays the selected digit.
		/// </summary>
		/// <param name="direction">The number of positions to move; positive values move forward and negative values move backward.</param>
		private void MoveCursor( int direction )
		{
			selectedDigit = ( selectedDigit + direction + DigitPositions.Length ) % DigitPositions.Length;
			showSelectedDigit = true;
		}

		/// <summary>
		/// Selects the field at the specified index, wrapping around the available fields, and displays its first digit.
		/// </summary>
		/// <param name="field">The index of the field to select.</param>
		private void SelectField( int field )
		{
			selectedField = ( field + FieldNames.Length ) % FieldNames.Length;
			selectedDigit = 0;
			showSelectedDigit = true;
		}

		/// <summary>
		/// Validates and saves the IPv4 address, subnet mask, and gateway settings.
		/// </summary>
		private void Save()
		{
			if( !TryGetIPv4Bytes( addresses[ 0 ], out byte[] deviceAddress )
				|| !TryGetIPv4Bytes( addresses[ 1 ], out byte[] subnetMask )
				|| !TryGetIPv4Bytes( addresses[ 2 ], out byte[] gatewayAddress ) )
			{
				hasInvalidOctet = true;

				Debug.WriteLine( "Network address octets must be between 0 and 255." );

				Render( true );
				return;
			}

			settings.EthernetSettings.Address = new IPAddress( deviceAddress );
			settings.EthernetSettings.SubnetMask = new IPAddress( subnetMask );
			settings.EthernetSettings.GatewayAddress = new IPAddress( gatewayAddress );

			string message = "Network saved";
			try
			{
				settings.Save();
			}
			catch( Exception ex )
			{
				Debug.WriteLine( "Network settings could not be persisted: " + ex.Message );
				message = "Not persisted";
			}

			isActive = false;

			display.SetLines( message, string.Empty );
			
			Thread.Sleep( 2000 );

			editingStopped();
		}

		/// <summary>
		/// Continuously renders the display while active, alternating the selected-digit state between renders.
		/// </summary>
		/// <remarks>Rendering is synchronized, with a 100-millisecond pause between iterations.</remarks>
		private void DisplayLoop()
		{
			while( IsActive )
			{
				lock( syncRoot )
				{
					if( isActive )
					{
						Render( showSelectedDigit );
						showSelectedDigit = !showSelectedDigit;
					}
				}

				Thread.Sleep( 100 );
			}
		}

		/// <summary>
		/// Displays the selected address field, optionally hiding the selected digit.
		/// </summary>
		/// <param name="showDigit">true to display the selected digit; otherwise, hides it.</param>
		private void Render( bool showDigit )
		{
			char[] text = ( char[] )addresses[ selectedField ].Clone();

			if( !showDigit )
			{
				text[ DigitPositions[ selectedDigit ] ] = ' ';
			}

			string title = hasInvalidOctet ? "Invalid IP octet" : $"{FieldNames[ selectedField ]} {selectedField + 1}/3";

			display.SetLines( title, new string( text ) );
		}

		/// <summary>
		/// Formats an IPv4 address as a zero-padded dotted-decimal character array.
		/// </summary>
		/// <param name="address">The IPv4 address to format.</param>
		/// <returns>A character array containing the address's four octets, each formatted as three digits and separated by periods.</returns>
		/// <exception cref="InvalidOperationException">The address does not contain four bytes.</exception>
		private static char[] FormatIPv4Address( IPAddress address )
		{
			byte[] bytes = address.GetAddressBytes();

			if( bytes.Length != 4 )
			{
				throw new InvalidOperationException( "Network settings must contain IPv4 addresses." );
			}

			return string.Format( "{0:D3}.{1:D3}.{2:D3}.{3:D3}", bytes[ 0 ], bytes[ 1 ], bytes[ 2 ], bytes[ 3 ] ).ToCharArray();
		}

		/// <summary>
		/// Parses four octet values from the character array into bytes.
		/// </summary>
		/// <param name="text">A character array containing the octet digits, with each octet starting four characters after the previous one.</param>
		/// <param name="bytes">When this method returns, contains the four parsed octet values.</param>
		/// <returns>true if none of the parsed octet values exceeds 255; otherwise, false.</returns>
		private static bool TryGetIPv4Bytes( char[] text, out byte[] bytes )
		{
			bytes = new byte[ 4 ];

			for( int octetIndex = 0; octetIndex < bytes.Length; octetIndex++ )
			{
				int firstDigit = octetIndex * 4;
				int value = ( text[ firstDigit ] - '0' ) * 100
					+ ( text[ firstDigit + 1 ] - '0' ) * 10
					+ text[ firstDigit + 2 ] - '0';

				if( value > byte.MaxValue )
				{
					return false;
				}

				bytes[ octetIndex ] = ( byte )value;
			}

			return true;
		}
	}
}
