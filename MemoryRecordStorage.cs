using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace THERadioReceiver
{
	/// <summary>
	/// Stores checksummed snapshots of named records in alternating memory banks.
	/// </summary>
	/// <param name="memory">The raw memory used to persist record snapshots.</param>
	internal sealed class MemoryRecordStorage( IRawMemory memory ) : IStorage, IDisposable
	{
		private const int HeaderSize = 20;
		private const uint Magic = 0x31525254;
		private readonly IRawMemory memory = memory ?? throw new ArgumentNullException( nameof( memory ) );
		private readonly object sync = new();
		private int BankSize => memory.Capacity / 2;

		/// <summary>
		/// Reads the record with the specified name.
		/// </summary>
		/// <param name="name">The name of the record to read.</param>
		/// <returns>The record data, or null if no record with that name exists.</returns>
		public byte[] Read( string name )
		{
			Persistence.ValidateName( name );

			lock( sync )
			{
				Hashtable records = Load( out _, out _ );
				return ( byte[] )records[ name ];
			}
		}

		/// <summary>
		/// Stores the specified data under the given name, replacing any existing record with that name.
		/// </summary>
		/// <param name="name">The name of the record to store.</param>
		/// <param name="data">The data to store.</param>
		/// <exception cref="ArgumentNullException">The value of <paramref name="data"/> is <see langword="null"/>.</exception>
		/// <exception cref="IOException">The storage capacity is exceeded, or the written data fails verification.</exception>
		public void Write( string name, byte[] data )
		{
			Persistence.ValidateName( name );

			if( data == null ) throw new ArgumentNullException( nameof( data ) );

			lock( sync )
			{
				Hashtable records = Load( out int active, out uint generation );
				records[ name ] = data;
				byte[] payload = Encode( records );

				if( payload.Length > BankSize - HeaderSize ) throw new IOException( "Storage capacity exceeded." );

				int address = ( active == 0 ? 1 : 0 ) * BankSize;
				byte[] header = new byte[ HeaderSize ];

				Put32( header, 4, unchecked( generation + 1 ) );
				Put32( header, 8, ( uint )payload.Length );
				Put32( header, 12, Checksum( payload ) );
				Put32( header, 16, Checksum( header, 4, 12 ) );

				memory.Write( address, new byte[ 4 ] );
				memory.Write( address + HeaderSize, payload );
				memory.Write( address + 4, Slice( header, 4, HeaderSize - 4 ) );

				Put32( header, 0, Magic );

				memory.Write( address, Slice( header, 0, 4 ) );

				byte[] verify = new byte[ HeaderSize + payload.Length ];

				memory.Read( address, verify );

				for( int i = 0; i < HeaderSize; i++ ) if( verify[ i ] != header[ i ] ) throw new IOException( "Storage write verification failed." );
				for( int i = 0; i < payload.Length; i++ ) if( verify[ HeaderSize + i ] != payload[ i ] ) throw new IOException( "Storage write verification failed." );
			}
		}

		/// <summary>
		/// Loads the most recent valid snapshot from either storage bank.
		/// </summary>
		/// <remarks>When only one snapshot is valid, that snapshot is loaded. If both snapshots are valid, the
		/// snapshot with the higher generation is loaded.</remarks>
		/// <param name="active">The index of the active storage bank, or -1 when no snapshot is available.</param>
		/// <param name="generation">The generation of the loaded snapshot.</param>
		/// <returns>The loaded snapshot, or a new empty <see cref="Hashtable"/> when no snapshot exists.</returns>
		/// <exception cref="IOException">Thrown when no valid storage snapshot is found and at least one bank contains unreadable or invalid data.</exception>
		private Hashtable Load( out int active, out uint generation )
		{
			Hashtable first = ReadBank( 0, out uint firstGeneration, out bool firstEmpty );
			Hashtable second = ReadBank( 1, out uint secondGeneration, out bool secondEmpty );

			if( first == null && second == null )
			{
				if( !firstEmpty || !secondEmpty ) throw new IOException( "No valid storage snapshot found." );
				active = -1; generation = 0; return new Hashtable();
			}
			
			if( ( first == null && !firstEmpty ) || ( second == null && !secondEmpty ) )
				Debug.WriteLine( "Invalid storage snapshot; using the remaining valid snapshot." );
			
			active = first == null ? 1 : second == null ? 0 : unchecked( ( int )( secondGeneration - firstGeneration ) ) > 0 ? 1 : 0;
			generation = active == 0 ? firstGeneration : secondGeneration;

			return ( active == 0 ? first : second )!;
		}

		/// <summary>
		/// Reads and decodes a storage bank.
		/// </summary>
		/// <remarks>The bank is rejected when its header or payload fails validation.</remarks>
		/// <param name="bank">The zero-based storage bank to read.</param>
		/// <param name="generation">When successful, the generation recorded in the bank header.</param>
		/// <param name="empty">When true, the bank contains no initialized data.</param>
		/// <returns>The decoded bank contents, or null when the bank is empty or invalid.</returns>
		private Hashtable ReadBank( int bank, out uint generation, out bool empty )
		{
			int address = bank * BankSize;
			byte[] header = new byte[ HeaderSize ];

			memory.Read( address, header );

			generation = 0;
			empty = true;
			bool allZero = true, allErased = true;

			foreach( byte b in header ) { allZero &= b == 0; allErased &= b == 0xff; }
			
			if( allZero || allErased || Get32( header, 0 ) == 0 ) return null!;
			
			empty = false;

			if( Get32( header, 0 ) != Magic || Get32( header, 16 ) != Checksum( header, 4, 12 ) ) return null!;
			
			uint length = Get32( header, 8 );
			
			if( length > BankSize - HeaderSize ) return null!;
			
			byte[] payload = new byte[ ( int )length ];
			
			memory.Read( address + HeaderSize, payload );
			
			if( Get32( header, 12 ) != Checksum( payload ) ) return null!;
			
			Hashtable result = Decode( payload );
			
			if( result == null ) return null!;
			
			generation = Get32( header, 4 );
			
			return result;
		}

		/// <summary>
		/// Encodes the records into a binary byte sequence.
		/// </summary>
		/// <remarks>Each record is encoded with the UTF-8 name length and value length as 32-bit values, followed by
		/// the name and value bytes.</remarks>
		/// <param name="records">The records containing UTF-8 names and byte values to encode.</param>
		/// <returns>A byte array containing the encoded record names and values.</returns>
		private static byte[] Encode( Hashtable records )
		{
			using MemoryStream stream = new();

			foreach( DictionaryEntry entry in records )
			{
				byte[] name = Encoding.UTF8.GetBytes( ( string )entry.Key );
				byte[] value = ( byte[] )entry.Value;
				byte[] lengths = new byte[ 8 ];

				Put32( lengths, 0, ( uint )name.Length ); Put32( lengths, 4, ( uint )value.Length );

				stream.Write( lengths, 0, lengths.Length ); stream.Write( name, 0, name.Length ); stream.Write( value, 0, value.Length );
			}

			return stream.ToArray();
		}

		/// <summary>
		/// Decodes record names and byte values from a binary data block.
		/// </summary>
		/// <remarks>Each record consists of a 32-bit name length, a 32-bit value length, an UTF-8 name, and the
		/// record value.</remarks>
		/// <param name="data">The binary data to decode.</param>
		/// <returns>A Hashtable containing unique record names and their byte values; otherwise, <see langword="null"/> if the data is
		/// malformed.</returns>
		private static Hashtable Decode( byte[] data )
		{
			Hashtable records = [];
			int offset = 0;

			while( offset < data.Length )
			{
				if( data.Length - offset < 8 ) return null!;

				uint nameLength = Get32( data, offset ), valueLength = Get32( data, offset + 4 ); offset += 8;
				
				if( nameLength == 0 || nameLength > data.Length - offset || valueLength > data.Length - offset - nameLength ) return null!;
				
				string name = Encoding.UTF8.GetString( Slice( data, offset, ( int )nameLength ) ); offset += ( int )nameLength;
				
				if( records.Contains( name ) ) return null!;
				
				records.Add( name, Slice( data, offset, ( int )valueLength ) ); offset += ( int )valueLength;
			}
			return records;
		}

		/// <summary>
		/// Copies a range of bytes from the source array.
		/// </summary>
		/// <param name="source">The array containing the bytes to copy.</param>
		/// <param name="offset">The zero-based index at which copying begins.</param>
		/// <param name="count">The number of bytes to copy.</param>
		/// <returns>A new array containing the copied bytes.</returns>
		private static byte[] Slice( byte[] source, int offset, int count )
		{
			byte[] result = new byte[ count ];

			Array.Copy( source, offset, result, 0, count ); 
			
			return result;
		}
		
		/// <summary>
		/// Writes an unsigned 32-bit value as four bytes in little-endian order.
		/// </summary>
		/// <param name="data">The byte array that receives the value.</param>
		/// <param name="offset">The zero-based index of the first byte to write.</param>
		/// <param name="value">The unsigned 32-bit value to write.</param>
		private static void Put32( byte[] data, int offset, uint value )
		{
			for( int i = 0; i < 4; i++ ) 
				data[ offset + i ] = ( byte )( value >> ( i * 8 ) ); 
		}

		/// <summary>
		/// Reads a 32-bit unsigned integer from a byte array.
		/// </summary>
		/// <param name="data">The byte array containing the value.</param>
		/// <param name="offset">The zero-based position of the first byte.</param>
		/// <returns>The 32-bit unsigned integer encoded in little-endian byte order.</returns>
		private static uint Get32( byte[] data, int offset )
		{
			uint value = 0;
			
			for( int i = 0; i < 4; i++ )
				value |= ( uint )data[ offset + i ] << ( i * 8 );

			return value;
		}

		/// <summary>
		/// Computes the checksum of the entire byte array.
		/// </summary>
		/// <param name="data">The data to compute the checksum for.</param>
		/// <returns>The checksum of the byte array.</returns>
		private static uint Checksum( byte[] data ) => Checksum( data, 0, data.Length );

		/// <summary>
		/// Computes the CRC-32 checksum of a segment of data.
		/// </summary>
		/// <remarks>Uses the reflected CRC-32 polynomial 0xEDB88320.</remarks>
		/// <param name="data">The byte array containing the data.</param>
		/// <param name="offset">The zero-based index of the first byte to include.</param>
		/// <param name="length">The number of bytes to include.</param>
		/// <returns>The CRC-32 checksum of the specified data segment.</returns>
		private static uint Checksum( byte[] data, int offset, int length )
		{
			uint crc = 0xffffffff;

			for( int i = offset; i < offset + length; i++ )
			{
				crc ^= data[ i ];
				for( int bit = 0; bit < 8; bit++ ) crc = ( crc >> 1 ) ^ ( ( crc & 1 ) != 0 ? 0xedb88320u : 0 );
			}

			return ~crc;
		}

		/// <summary>
		/// Releases the resources held by the memory.
		/// </summary>
		public void Dispose() => memory.Dispose();
	}
}
