using System;
using System.Collections;

namespace THERadioReceiver
{
	/// <summary>
	/// Provides methods for serializing and deserializing objects.
	/// </summary>
	public interface ISerializer
	{
		byte[] Serialize( object value );
		object Deserialize( byte[] data, Type targetType );
	}

	/// <summary>
	/// Provides read and write access to named byte data.
	/// </summary>
	/// <remarks>A missing record returns null. Read failures throw an exception.</remarks>
	public interface IStorage
	{
		// A missing record returns null; read failures must throw.
		byte[] Read( string name );
		void Write( string name, byte[] data );
	}

	/// <summary>
	/// Provides persistence operations for saving and loading serialized records.
	/// </summary>
	/// <param name="storage">The storage used to read and write records.</param>
	/// <param name="serializer">The serializer used to convert values to and from stored data.</param>
	public sealed class Persistence( IStorage storage, ISerializer serializer )
	{
		private readonly IStorage storage = storage ?? throw new ArgumentNullException( nameof( storage ) );
		private readonly ISerializer serializer = serializer ?? throw new ArgumentNullException( nameof( serializer ) );

		/// <summary>
		/// Serializes and stores a value under the specified name.
		/// </summary>
		/// <param name="name">The name under which to store the value.</param>
		/// <param name="value">The value to serialize and store.</param>
		/// <exception cref="ArgumentNullException">The value is null.</exception>
		public void Save( string name, object value )
		{
			ValidateName( name );
			
			if( value == null ) throw new ArgumentNullException( nameof( value ) );
			
			storage.Write( name, serializer.Serialize( value ) );
		}

		/// <summary>
		/// Loads the named persistent record.
		/// </summary>
		/// <param name="name">The name of the persistent record to load.</param>
		/// <param name="targetType">The type of the value to load.</param>
		/// <returns>The loaded value.</returns>
		/// <exception cref="InvalidOperationException">The persistent record could not be found.</exception>
		public object Load( string name, Type targetType )
		{
			if( !TryLoad( name, targetType, out object value ) )
				throw new InvalidOperationException( "Persistent record not found: " + name );
			
				return value;
		}

		/// <summary>
		/// Loads and deserializes the named persistent record as the specified type.
		/// </summary>
		/// <param name="name">The name of the persistent record to load.</param>
		/// <param name="targetType">The type to deserialize the record as.</param>
		/// <param name="value">When this method returns <see langword="true"/>, contains the deserialized record; otherwise, <see
		/// langword="null"/>.</param>
		/// <returns><see langword="true"/> if the record is found and deserialized successfully; otherwise, <see langword="false"/>.</returns>
		/// <exception cref="ArgumentNullException">The <paramref name="targetType"/> is <see langword="null"/>.</exception>
		/// <exception cref="InvalidOperationException">The record deserializes to <see langword="null"/> or to a value that is not an instance of <paramref
		/// name="targetType"/>.</exception>
		public bool TryLoad( string name, Type targetType, out object value )
		{
			ValidateName( name );

			if( targetType == null ) throw new ArgumentNullException( nameof( targetType ) );
			
			byte[] data = storage.Read( name );
			
			if( data == null )
			{
				value = null!;
				return false;
			}
			value = serializer.Deserialize( data, targetType );
			
			if( value == null || !targetType.IsInstanceOfType( value ) )
				throw new InvalidOperationException( "Invalid persistent record: " + name );
			
			return true;
		}

		/// <summary>
		/// Validates that a record name is provided.
		/// </summary>
		/// <param name="name">The record name to validate.</param>
		/// <exception cref="ArgumentException">The name is null or empty.</exception>
		internal static void ValidateName( string name )
		{
			if( string.IsNullOrEmpty( name ) ) throw new ArgumentException( "A record name is required.", nameof( name ) );
		}
	}

	/// <summary>
	/// Volatile, in-memory stand-in for FRAM storage.
	/// </summary>
	/// <remarks>Records are available only for the lifetime of the instance. Reads and writes are synchronized, and
	/// stored data is copied to prevent changes through returned or supplied arrays.</remarks>
	public sealed class FramStorageDummy : IStorage
	{
		private readonly Hashtable records = [];
		private readonly object sync = new();

		/// <summary>
		/// Reads the record associated with the specified name.
		/// </summary>
		/// <remarks>The returned array is independent of the stored data.</remarks>
		/// <param name="name">The name of the record to read.</param>
		/// <returns>A copy of the record data, or null if no record exists with the specified name.</returns>
		public byte[] Read( string name )
		{
			Persistence.ValidateName( name );

			lock( sync )
			{
				byte[] data = ( byte[] )records[ name ];

				return data == null ? null! : Copy( data );
			}
		}

		/// <summary>
		/// Stores a copy of the data under the specified name.
		/// </summary>
		/// <param name="name">The name associated with the data.</param>
		/// <param name="data">The data to store.</param>
		/// <exception cref="ArgumentNullException">data is null.</exception>
		public void Write( string name, byte[] data )
		{
			Persistence.ValidateName( name );

			if( data == null ) throw new ArgumentNullException( nameof( data ) );

			lock( sync ) records[ name ] = Copy( data );
		}

		/// <summary>
		/// Creates a copy of the specified byte array.
		/// </summary>
		/// <param name="data">The byte array to copy.</param>
		/// <returns>A new byte array containing the same bytes as `data`.</returns>
		private static byte[] Copy( byte[] data )
		{
			byte[] copy = new byte[ data.Length ];

			Array.Copy( data, copy, data.Length );
			
			return copy;
		}
	}
}
