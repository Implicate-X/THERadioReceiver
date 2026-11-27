using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using GHIElectronics.TinyCLR.Devices.Gpio;
using ImplicateX.TinyCLR.Drivers.Decoder;
using ImplicateX.TinyCLR.Drivers.Expander;

namespace THERadioReceiver
{
	/// <summary>
	/// Coordinates web radio playback for the connected chassis.
	/// </summary>
	/// <param name="chassis">The chassis that provides board hardware and persistent storage.</param>
	public sealed partial class WebRadio( Chassis chassis )
	{
		public Chassis Chassis { get; private set; } = chassis;
		public Station CurrentStation { get; private set; } = null!;
		private volatile bool linkReady = false;
		private GpioController gpioController = null!;
		private Mcp23017 gpioExpander = null!;

		/// <summary>
		/// Gets the Web stations.
		/// </summary>
		private Stations stations = null!;

		/// <summary>
		/// Initializes the GPIO resources, loads saved stations, and selects the first station as current.
		/// </summary>
		/// <remarks>The station collection must contain at least one station.</remarks>
		public void Initialize()
		{
			gpioController = Chassis.Board.GpioController;
			gpioExpander = Chassis.Board.GpioExpander;

			stations = new Stations( new Persistence( Chassis.Storage.East, new BsonSerializer() ) );
			stations.Load();

			CurrentStation = stations.Items[ 0 ];
		}

		/// <summary>
		/// Starts playback of the current radio station.
		/// </summary>
		/// <param name="codec">The VS1053 codec that streams and plays the radio audio.</param>
		/// <exception cref="InvalidOperationException">The network link is not connected.</exception>
		public void StartPlayback( Vs1053 codec )
		{
			if( !linkReady )
			{
				throw new InvalidOperationException( "Network link is not connected." );
			}

			var source = new RadioAudioSource( CurrentStation.Url );

			try
			{
				codec.StartBufferedStream( source, loadStandardCodecPatch: false );
			}
			catch
			{
				source.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Provides an HTTP audio source for an MP3 radio stream.
		/// </summary>
		/// <remarks>Follows up to four redirects and rejects redirects from HTTPS to HTTP. The response must have an
		/// audio/mpeg content type. The request can be cancelled or disposed.</remarks>
		/// <param name="url">The URL of the radio stream.</param>
		private sealed class RadioAudioSource( string url ) : Vs1053.IBufferedAudioSource
		{
			private const int MaxRedirects = 4;
			private readonly object sync = new();
			private readonly string initialUrl = url;
			private HttpWebRequest request = null!;
			private HttpWebResponse response = null!;
			private bool cancelled;
			private bool disposed;

			/// <summary>
			/// Opens the radio stream and follows valid HTTP redirects.
			/// </summary>
			/// <returns>A stream containing MP3 audio.</returns>
			/// <exception cref="InvalidOperationException">The request was cancelled or disposed, the response is not MP3 audio, the server returned an unexpected status,
			/// or a redirect is invalid or exceeds the maximum allowed.</exception>
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
						Debug.WriteLine( "Radio stream content type: " + currentResponse.ContentType ); Debug.WriteLine( "=== Stream Headers ===" );

						foreach( var header in currentResponse.Headers.AllKeys )
						{
							Debug.WriteLine(
								$"{header} = {currentResponse.Headers[header]}"
							);

							foreach( var value in currentResponse.Headers.GetValues( header ) )
							{
								Debug.WriteLine( $"  {value}" );
							}
						}

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

			/// <summary>
			/// Cancels the active radio request.
			/// </summary>
			/// <remarks>No effect is applied after the instance has been disposed.</remarks>
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

			/// <summary>
			/// Releases the response and request resources.
			/// </summary>
			/// <remarks>Subsequent calls have no effect.</remarks>
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
