using System;
using System.Text;
using GHIElectronics.TinyCLR.Data.Json;

namespace THERadioReceiver
{
	/// <summary>
	/// Serializes objects to JSON and deserializes JSON data from UTF-8 encoded byte arrays.
	/// </summary>
	public sealed class JsonSerializer : ISerializer
	{
		/// <summary>
		/// Serializes a value to a UTF-8 encoded JSON byte array.
		/// </summary>
		/// <param name="value">The value to serialize.</param>
		/// <returns>A byte array containing the UTF-8 encoded JSON representation of <paramref name="value"/>.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
		public byte[] Serialize( object value )
		{
			if( value == null ) throw new ArgumentNullException( nameof( value ) );

			return Encoding.UTF8.GetBytes( JsonConverter.Serialize( value ).ToString() );
		}

		/// <summary>
		/// Deserializes UTF-8 encoded JSON data into an object of the specified type.
		/// </summary>
		/// <param name="data">The UTF-8 encoded JSON data to deserialize.</param>
		/// <param name="targetType">The type of object to deserialize the data into.</param>
		/// <returns>An object of the specified type containing the deserialized data.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="targetType"/> is <see langword="null"/>.</exception>
		public object Deserialize( byte[] data, Type targetType )
		{
			if( data == null ) throw new ArgumentNullException( nameof( data ) );
			if( targetType == null ) throw new ArgumentNullException( nameof( targetType ) );

			return JsonConverter.DeserializeObject( Encoding.UTF8.GetString( data ), targetType );
		}
	}
}
