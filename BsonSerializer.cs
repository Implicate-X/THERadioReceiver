using System;
using System.Collections;
using System.IO;
using System.Text;
using GHIElectronics.TinyCLR.Data.Json;

namespace THERadioReceiver
{
	/// <summary>
	/// Serializes JSON-compatible values to and from BSON.
	/// </summary>
	/// <remarks>Serialized values are stored in a BSON document with a single member named `value`. Deserialization
	/// requires that member and rejects unsupported BSON types or malformed documents.</remarks>
	public sealed class BsonSerializer : ISerializer
	{
		public byte[] Serialize( object value )
		{
			if( value == null ) throw new ArgumentNullException( nameof( value ) );
			// Parse the JSON representation to normalize byte and other small numeric values.
			JToken token = JsonConverter.Deserialize( JsonConverter.Serialize( value ).ToString() );
			JObject root = new();

			root.Add( "value", token );

			return WriteDocument( root );
		}

		/// <summary>
		/// Deserializes the value in a BSON persistence document to the specified type.
		/// </summary>
		/// <param name="data">The BSON persistence document to deserialize.</param>
		/// <param name="targetType">The type to deserialize the document value into.</param>
		/// <returns>The deserialized value.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="targetType"/> is <see langword="null"/>.</exception>
		/// <exception cref="FormatException">Thrown when <paramref name="data"/> does not contain a valid BSON persistence document.</exception>
		public object Deserialize( byte[] data, Type targetType )
		{
			if( data == null ) throw new ArgumentNullException( nameof( data ) );
			if( targetType == null ) throw new ArgumentNullException( nameof( targetType ) );

			int offset = 0;

			JObject root = ReadDocument( data, ref offset, data.Length );

			if( offset != data.Length || root.Members.Count != 1 || !root.Contains( "value" ) )
				throw new FormatException( "Invalid BSON persistence document." );

			return JsonConverter.DeserializeObject( root[ "value" ].Value.ToString(), targetType );
		}

		/// <summary>
		/// Writes a BSON document from an object or array token.
		/// </summary>
		/// <param name="token">The object or array token to serialize.</param>
		/// <returns>The BSON-encoded document as a byte array.</returns>
		/// <exception cref="FormatException">The token is neither an object nor an array.</exception>
		private static byte[] WriteDocument( JToken token )
		{
			using MemoryStream body = new();

			if( token is JObject obj )
			{
				foreach( JProperty property in obj.Members )
					WriteElement( body, property.Name, property.Value );
			}
			else if( token is JArray array )
			{
				for( int i = 0; i < array.Length; i++ )
					WriteElement( body, i.ToString(), array[ i ] );
			}
			else throw new FormatException( "BSON documents must contain objects or arrays." );

			byte[] contents = body.ToArray();
			using MemoryStream result = new();

			WriteInt32( result, checked(contents.Length + 5) );

			result.Write( contents, 0, contents.Length );
			result.WriteByte( 0 );

			return result.ToArray();
		}

		/// <summary>
		/// Writes a BSON element to the specified stream.
		/// </summary>
		/// <param name="stream">The stream to which the BSON element is written.</param>
		/// <param name="name">The element name.</param>
		/// <param name="token">The JSON token whose value is written.</param>
		/// <exception cref="FormatException">The token is not a JSON object, array, or scalar value.</exception>
		/// <exception cref="NotSupportedException">The token contains an unsupported value, or an unsigned integer exceeds <see cref="long.MaxValue"/>.</exception>
		private static void WriteElement( MemoryStream stream, string name, JToken token )
		{
			byte type;
			object value = token is JValue scalar ? scalar.Value : null!;

			(type, value) = token switch
			{
				JObject => (( byte )BsonTypes.BsonDocument, ( object )token),
				JArray => (( byte )BsonTypes.BsonArray, ( object )token),
				JValue { Value: null } => (( byte )BsonTypes.BsonNull, null!),
				JValue { Value: string } => (( byte )BsonTypes.BsonString, value),
				JValue { Value: bool } => (( byte )BsonTypes.BsonBoolean, value),
				JValue { Value: int } => (( byte )BsonTypes.BsonInt32, value),
				JValue { Value: long } => (( byte )BsonTypes.BsonInt64, value),
				JValue { Value: ulong unsignedValue } when unsignedValue <= long.MaxValue => (( byte )BsonTypes.BsonInt64, ( object )( long )unsignedValue),
				JValue { Value: double } => (( byte )BsonTypes.BsonDouble, value),
				_ => throw new NotSupportedException( "Unsupported BSON value: " + value?.GetType().FullName )
			};

			stream.WriteByte( type );

			WriteCString( stream, name );

			switch( type )

			{
				case ( byte )BsonTypes.BsonDocument:
				case ( byte )BsonTypes.BsonArray:
					byte[] child = WriteDocument( token );
					stream.Write( child, 0, child.Length );
					break;

				case ( byte )BsonTypes.BsonString:
					byte[] text = Encoding.UTF8.GetBytes( ( string )value );
					WriteInt32( stream, checked(text.Length + 1) );
					stream.Write( text, 0, text.Length );
					stream.WriteByte( 0 );
					break;

				case ( byte )BsonTypes.BsonBoolean:
					stream.WriteByte( ( bool )value ? ( byte )1 : ( byte )0 );
					break;

				case ( byte )BsonTypes.BsonInt32:
					WriteInt32( stream, ( int )value );
					break;

				case ( byte )BsonTypes.BsonInt64:
					WriteInt64( stream, ( long )value );
					break;

				case ( byte )BsonTypes.BsonDouble:
					WriteInt64( stream, BitConverter.DoubleToInt64Bits( ( double )value ) );
					break;
			}
		}

		/// <summary>
		/// Writes a UTF-8 encoded, null-terminated string to the stream.
		/// </summary>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="value">The string to encode and write.</param>
		/// <exception cref="FormatException">The string contains a null character.</exception>
		private static void WriteCString( MemoryStream stream, string value )
		{
			if( value.IndexOf( '\0' ) >= 0 ) throw new FormatException( "BSON names cannot contain null characters." );

			byte[] bytes = Encoding.UTF8.GetBytes( value );
			
			stream.Write( bytes, 0, bytes.Length );
			stream.WriteByte( 0 );
		}

		/// <summary>
		/// Writes a 32-bit integer to the stream in little-endian byte order.
		/// </summary>
		/// <remarks>Writes exactly four bytes.</remarks>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="value">The 32-bit integer to write.</param>
		private static void WriteInt32( MemoryStream stream, int value )
		{
			for( int i = 0; i < 4; i++ ) stream.WriteByte( ( byte )( value >> ( i * 8 ) ) );
		}

		/// <summary>
		/// Writes a 64-bit integer to the stream in little-endian byte order.
		/// </summary>
		/// <remarks>Writes exactly eight bytes.</remarks>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="value">The 64-bit integer to write.</param>
		private static void WriteInt64( MemoryStream stream, long value )
		{
			for( int i = 0; i < 8; i++ ) stream.WriteByte( ( byte )( value >> ( i * 8 ) ) );
		}

		/// <summary>
		/// Reads a BSON document from the specified byte array.
		/// </summary>
		/// <param name="data">The byte array containing the BSON document.</param>
		/// <param name="offset">The zero-based index at which to begin reading; updated to the index immediately following the document.</param>
		/// <param name="limit">The exclusive upper bound for reading from <paramref name="data"/>.</param>
		/// <returns>The BSON document as a <see cref="JObject"/>.</returns>
		/// <exception cref="FormatException">The document has an invalid length or terminator, or contains duplicate member names.</exception>
		private static JObject ReadDocument( byte[] data, ref int offset, int limit )
		{
			int start = offset;
			int length = ReadInt32( data, ref offset, limit );

			if( length < 5 || length > limit - start ) throw new FormatException( "Invalid BSON document length." );
			
			int end = start + length;

			JObject result = new();

			while( offset < end - 1 )
			{
				byte type = data[ offset++ ];

				string name = ReadCString( data, ref offset, end - 1 );

				if( result.Contains( name ) ) throw new FormatException( "Duplicate BSON member: " + name );

				result.Add( name, ReadValue( data, ref offset, end - 1, type ) );
			}

			if( offset != end - 1 || data[ offset++ ] != 0 ) throw new FormatException( "Invalid BSON document terminator." );

			return result;
		}

		/// <summary>
		/// Reads a BSON value from the specified data.
		/// </summary>
		/// <param name="data">The BSON data to read.</param>
		/// <param name="offset">The zero-based position in data at which reading begins; advanced past the value read.</param>
		/// <param name="limit">The exclusive upper bound for reading in data.</param>
		/// <param name="type">The BSON element type code identifying the value to read.</param>
		/// <returns>The token representing the BSON value.</returns>
		/// <exception cref="FormatException">The value is malformed or the BSON element type is unsupported.</exception>
		private static JToken ReadValue( byte[] data, ref int offset, int limit, byte type )
		{
			switch( type )
			{
				case ( byte )BsonTypes.BsonDocument: return ReadDocument( data, ref offset, limit );
				case ( byte )BsonTypes.BsonArray:
					JObject items = ReadDocument( data, ref offset, limit );
					JToken[] values = new JToken[ items.Members.Count ];

					int index = 0;
					
					foreach( JProperty item in items.Members )
					{
						if( item.Name != index.ToString() ) throw new FormatException( "Invalid BSON array index." );
						values[ index++ ] = item.Value;
					}
					
					return new JArray( values );

				case ( byte )BsonTypes.BsonString:
					int length = ReadInt32( data, ref offset, limit );
					
					if( length < 1 || length > limit - offset || data[ offset + length - 1 ] != 0 ) throw new FormatException( "Invalid BSON string." );
					
					string text = Encoding.UTF8.GetString( data, offset, length - 1 );
					offset += length;

					return new JValue( text );

				case ( byte )BsonTypes.BsonBoolean:
					RequireBytes( offset, 1, limit );

					byte boolean = data[ offset++ ];
					
					if( boolean > 1 ) throw new FormatException( "Invalid BSON boolean." );
					
					return new JValue( boolean == 1 );

				case ( byte )BsonTypes.BsonNull: return new JValue();
				case ( byte )BsonTypes.BsonInt32: return new JValue( ReadInt32( data, ref offset, limit ) );
				case ( byte )BsonTypes.BsonInt64: return new JValue( ReadInt64( data, ref offset, limit ) );
				case ( byte )BsonTypes.BsonDouble: return new JValue( BitConverter.Int64BitsToDouble( ReadInt64( data, ref offset, limit ) ) );
				default: throw new FormatException( "Unsupported BSON element type: " + type );
			}
		}

		/// <summary>
		/// Reads a null-terminated UTF-8 string from the specified data.
		/// </summary>
		/// <param name="data">The byte array containing the string.</param>
		/// <param name="offset">The zero-based index at which to begin reading; advanced past the null terminator.</param>
		/// <param name="limit">The exclusive upper bound for reading in <paramref name="data"/>.</param>
		/// <returns>The decoded string.</returns>
		/// <exception cref="FormatException">The string has no null terminator before <paramref name="limit"/>.</exception>
		private static string ReadCString( byte[] data, ref int offset, int limit )
		{
			int start = offset;

			while( offset < limit && data[ offset ] != 0 ) offset++;

			if( offset == limit ) throw new FormatException( "Missing BSON name terminator." );

			string value = Encoding.UTF8.GetString( data, start, offset - start );
			offset++;

			return value;
		}

		/// <summary>
		/// Reads a 32-bit signed integer in little-endian byte order.
		/// </summary>
		/// <param name="data">The byte array containing the integer.</param>
		/// <param name="offset">The zero-based position to read from; advanced by four after the read.</param>
		/// <param name="limit">The exclusive upper bound for the read.</param>
		/// <returns>The 32-bit signed integer read from the byte array.</returns>
		private static int ReadInt32( byte[] data, ref int offset, int limit )
		{
			RequireBytes( offset, 4, limit );

			int value = data[ offset ] | data[ offset + 1 ] << 8 | data[ offset + 2 ] << 16 | data[ offset + 3 ] << 24;
			offset += 4;

			return value;
		}

		/// <summary>
		/// Reads a signed 64-bit integer in little-endian byte order.
		/// </summary>
		/// <param name="data">The byte array to read from.</param>
		/// <param name="offset">The zero-based position to read from; advanced by eight after the read.</param>
		/// <param name="limit">The exclusive upper bound for the read.</param>
		/// <returns>The signed 64-bit integer read from the byte array.</returns>
		private static long ReadInt64( byte[] data, ref int offset, int limit )
		{
			RequireBytes( offset, 8, limit );

			long value = 0;

			for( int i = 0; i < 8; i++ ) value |= ( long )data[ offset + i ] << ( i * 8 );

			offset += 8;

			return value;
		}

		/// <summary>
		/// Validates that the requested byte range is within the specified limit.
		/// </summary>
		/// <param name="offset">The starting offset of the byte range.</param>
		/// <param name="count">The number of bytes required.</param>
		/// <param name="limit">The limit of the available byte range.</param>
		/// <exception cref="FormatException">The offset is negative or the requested byte range exceeds the limit.</exception>
		private static void RequireBytes( int offset, int count, int limit )
		{
			if( offset < 0 || count > limit - offset ) throw new FormatException( "Truncated BSON document." );
		}
	}
}
