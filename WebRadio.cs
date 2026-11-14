using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.IO;
using ImplicateX.TinyCLR.Drivers.Decoder;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Devices.Network;
using GHIElectronics.TinyCLR.Devices.Spi;
using ImplicateX.TinyCLR.Drivers.Expander;

namespace THERadioReceiver
{
	public sealed class WebRadio( Chassis chassis )
	{
		private const string TestStreamUrl = "http://wdr-wdr5-live.icecast.wdr.de/wdr/wdr5/live/mp3/128/stream.mp3";
		public const string TestStationName = "Bremen Zwei";
		public const string TestStationFormat = "MP3 128 kbit/s";
		private volatile bool linkReady = false;
		private GpioController gpioController = null!;
		private Mcp23017 gpioExpander = null!;

		public void Initialize()
		{
			gpioController = chassis.Board.GpioController;
			gpioExpander = chassis.Board.GpioExpander;

			var networkController = NetworkController.FromName( SC20100.NetworkController.Enc28j60 );

			var cs = gpioController.OpenPin( Board.ControllerPin.EthernetCS );

			var spiSettings = new SpiConnectionSettings
			{
				ChipSelectLine = cs,
				ClockFrequency = 4_000_000,
				Mode = SpiMode.Mode0,
				ChipSelectType = SpiChipSelectType.Gpio,
				ChipSelectHoldTime = TimeSpan.FromTicks( 10 ),
				ChipSelectSetupTime = TimeSpan.FromTicks( 10 ),
			};

			var networkCommunicationInterfaceSettings = new SpiNetworkCommunicationInterfaceSettings
			{
				SpiApiName = FEZFeather.SpiBus.Spi4,
				GpioApiName = SC20100.GpioPin.Id,
				SpiSettings = spiSettings,
				InterruptPin = gpioController.OpenPin( Board.ControllerPin.EthernetINT ),
				InterruptEdge = GpioPinEdge.FallingEdge,
				InterruptDriveMode = GpioPinDriveMode.InputPullUp,
				ResetPin = gpioController.OpenPin( Board.ControllerPin.EthernetReset ),
				ResetActiveState = GpioPinValue.Low,
			};

			var networkInterfaceSetting = new EthernetNetworkInterfaceSettings
			{
				Address = new IPAddress( [ 192, 168, 1, 53 ] ),
				SubnetMask = new IPAddress( [ 255, 255, 255, 0 ] ),
				GatewayAddress = new IPAddress( [ 192, 168, 1, 1 ] ),
				DnsAddresses = [ new( [ 192, 168, 1, 1 ] ), new( [ 192, 168, 1, 254 ] ), ],
				MacAddress = [ 0x00, 0x04, 0x00, 0x00, 0x00, 0x00 ],
				DhcpEnable = false,
				DynamicDnsEnable = false
			};

			networkController.SetInterfaceSettings( networkInterfaceSetting );
			networkController.SetCommunicationInterfaceSettings( networkCommunicationInterfaceSettings );
			networkController.SetAsDefaultController();

			networkController.NetworkAddressChanged += NetworkController_NetworkAddressChanged; ;
			networkController.NetworkLinkConnectedChanged += NetworkController_NetworkLinkConnectedChanged; ;

			networkController.Enable();

			while( !linkReady )
			{
				Thread.Sleep( 10 );
			}
			Debug.WriteLine( "Network is ready to use" );
		}

		public void StartPlayback( Vs1053 codec )
		{
			if( !linkReady )
			{
				throw new InvalidOperationException( "Network link is not connected." );
			}

			var source = new RadioAudioSource( TestStreamUrl );

			try
			{
				codec.StartBufferedStream( source );
			}
			catch
			{
				source.Dispose();
				throw;
			}
		}

		private sealed class RadioAudioSource : Vs1053.IBufferedAudioSource
		{
			private const int MaxRedirects = 4;
			private readonly object sync = new();
			private readonly string initialUrl;
			private HttpWebRequest request = null!;
			private HttpWebResponse response = null!;
			private bool cancelled;
			private bool disposed;

			public RadioAudioSource( string url )
			{
				this.initialUrl = url;
			}

			public Stream Open()
			{
				string url = this.initialUrl;

				for( int redirects = 0; ; redirects++ )
				{
					HttpWebRequest currentRequest;

					lock( this.sync )
					{
						if( this.cancelled || this.disposed )
						{
							throw new InvalidOperationException( "Radio stream request was cancelled." );
						}

						currentRequest = ( HttpWebRequest )WebRequest.Create( url );
						currentRequest.AllowAutoRedirect = false;
						currentRequest.KeepAlive = false;
						currentRequest.Timeout = 10000;
						currentRequest.ReadWriteTimeout = 10000;
						this.request = currentRequest;
					}

					HttpWebResponse currentResponse = ( HttpWebResponse )currentRequest.GetResponse();
					this.response = currentResponse;

					Debug.WriteLine( "Radio stream HTTP status: " + currentResponse.StatusCode );

					if( currentResponse.StatusCode == HttpStatusCode.OK )
					{
						Debug.WriteLine( "Radio stream content type: " + currentResponse.ContentType );

						if( currentResponse.ContentType == null || currentResponse.ContentType.ToLower().IndexOf( "audio/mpeg" ) != 0 )
						{
							throw new InvalidOperationException( "Radio stream did not return MP3 audio." );
						}
						return currentResponse.GetResponseStream();
					}

					int status = ( int )currentResponse.StatusCode;

					if( status != 301 && status != 302 && status != 303 && status != 307 && status != 308 )
					{
						throw new InvalidOperationException( "Unexpected radio stream HTTP status: " + status );
					}

					if( redirects >= MaxRedirects )
					{
						throw new InvalidOperationException( "Too many radio stream redirects." );
					}

					string location = currentResponse.Headers[ "Location" ];

					if( string.IsNullOrEmpty( location ) )
					{
						throw new InvalidOperationException( "Radio stream redirect has no Location header." );
					}

					var target = new Uri( location );

					if( !target.IsAbsoluteUri || ( target.Scheme != "http" && target.Scheme != "https" ) )
					{
						throw new InvalidOperationException( "Radio stream redirect must be an absolute HTTP or HTTPS URL." );
					}

					if( currentRequest.RequestUri.Scheme == "https" && target.Scheme == "http" )
					{
						throw new InvalidOperationException( "Radio stream redirect from HTTPS to HTTP is not allowed." );
					}

					url = location;

					lock( this.sync )
					{
						try
						{
							currentResponse.Dispose();
						}
						finally
						{
							this.response = null!;
							this.request = null!;

							currentRequest.Dispose();
						}
					}
				}
			}

			public void Cancel()
			{
				lock( this.sync )
				{
					if( !this.disposed )
					{
						this.cancelled = true;
						this.request?.Abort();
					}
				}
			}

			public void Dispose()
			{
				lock( this.sync )
				{
					if( !this.disposed )
					{
						this.disposed = true;
						try
						{
							this.response?.Dispose();
						}
						finally
						{
							this.request?.Dispose();
						}
					}
				}
			}
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


//Deutschlandfunk (DLF), MP3 (128 kbit / s),[ http://st01.sslstream.dlf.de/dlf/01/128/mp3/stream.mp3](http://st01.sslstream.dlf.de/dlf/01/128/mp3/stream.mp3)
//DLF Kultur, MP3 (128 kbit / s),[ http://st02.sslstream.dlf.de/dlf/02/128/mp3/stream.mp3](http://st02.sslstream.dlf.de/dlf/02/128/mp3/stream.mp3)
//DLF Nova, MP3 (128 kbit / s),[ http://st03.sslstream.dlf.de/dlf/03/128/mp3/stream.mp3](http://st03.sslstream.dlf.de/dlf/03/128/mp3/stream.mp3)
//WDR 2 (Rheinland), MP3 (128 kbit / s),[ http://wdr-wdr2-rheinland.icecast.wdr.de/wdr/wdr2/rheinland/mp3/128/stream.mp3](http://wdr-wdr2-rheinland.icecast.wdr.de/wdr/wdr2/rheinland/mp3/128/stream.mp3)
//WDR 5, MP3 (128 kbit / s),[ http://wdr-wdr5-live.icecast.wdr.de/wdr/wdr5/live/mp3/128/stream.mp3](http://wdr-wdr5-live.icecast.wdr.de/wdr/wdr5/live/mp3/128/stream.mp3)
//1LIVE( WDR ),MP3( 128 kbit / s ),[http://wdr-1live-live.icecast.wdr.de/wdr/1live/live/mp3/128/stream.mp3](http://wdr-1live-live.icecast.wdr.de/wdr/1live/live/mp3/128/stream.mp3)
//NDR 2, MP3( 128 kbit / s ),[http://ndr-ndr2-niedersachsen.cast.addradio.de/ndr/ndr2/niedersachsen/mp3/128/stream.mp3](http://ndr-ndr2-niedersachsen.cast.addradio.de/ndr/ndr2/niedersachsen/mp3/128/stream.mp3)
//NDR Info, MP3( 128 kbit / s ),[http://ndr-ndrinfo-niedersachsen.cast.addradio.de/ndr/ndrinfo/niedersachsen/mp3/128/stream.mp3](http://ndr-ndrinfo-niedersachsen.cast.addradio.de/ndr/ndrinfo/niedersachsen/mp3/128/stream.mp3)
//N-JOY( NDR ), MP3( 128 kbit / s ),[http://ndr-njoy-live.cast.addradio.de/ndr/njoy/live/mp3/128/stream.mp3](http://ndr-njoy-live.cast.addradio.de/ndr/njoy/live/mp3/128/stream.mp3)
