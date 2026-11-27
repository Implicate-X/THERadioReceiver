using System;
using GHIElectronics.TinyCLR.Devices.I2c;
using ImplicateX.TinyCLR.Drivers.Memory;

namespace THERadioReceiver
{
	/// <summary>
	/// Provides three independent storage locations for named byte records.
	/// </summary>
	/// <remarks>Disposing the instance disposes each storage location that implements <see
	/// cref="IDisposable"/>.</remarks>
	/// <param name="north">The north storage location.</param>
	/// <param name="west">The west storage location.</param>
	/// <param name="east">The east storage location.</param>
	public sealed class Storage( IStorage north, IStorage west, IStorage east ) : IDisposable
	{
		public IStorage North { get; } = north ?? throw new ArgumentNullException( nameof( north ) );
		public IStorage West { get; } = west ?? throw new ArgumentNullException( nameof( west ) );
		public IStorage East { get; } = east ?? throw new ArgumentNullException( nameof( east ) );

		/// <summary>
		/// Creates storage backed by an EEPROM and two FRAM devices.
		/// </summary>
		/// <param name="controller">The I²C controller used to access the memory devices.</param>
		/// <returns>A storage instance containing record storage for the EEPROM and both FRAM devices.</returns>
		public static Storage CreateDefault( I2cController controller )
		{
			return new Storage(
				new MemoryRecordStorage( new EepromMemory( controller, 0x54 ) ),
				new MemoryRecordStorage( new FramMemory( controller, 0x56 ) ),
				new MemoryRecordStorage( new FramMemory( controller, 0x57 ) ) );
		}

		/// <summary>
		/// Disposes the North, West, and East objects if they implement <see cref="IDisposable"/>.
		/// </summary>
		public void Dispose()
		{
			( North as IDisposable )?.Dispose();
			( West as IDisposable )?.Dispose();
			( East as IDisposable )?.Dispose();
		}
	}

	/// <summary>
	/// Provides read and write access to a memory region.
	/// </summary>
	internal interface IRawMemory : IDisposable
	{
		int Capacity { get; }
		void Read( int address, byte[] buffer );
		void Write( int address, byte[] buffer );
	}

	/// <summary>
	/// Represents EEPROM memory accessed through an I2C controller.
	/// </summary>
	/// <param name="controller">The I2C controller used to communicate with the EEPROM.</param>
	/// <param name="address">The I2C address of the EEPROM device.</param>
	internal sealed class EepromMemory( I2cController controller, int address ) : IRawMemory
	{
		private readonly I2cController controller = controller;
		private readonly int address = address;
		private At24c256v device = null!;

		public int Capacity => At24c256v.CapacityBytes;
		private At24c256v Device => device ??= new At24c256v( controller, address );
		public void Read( int address, byte[] buffer ) => Device.Read( address, buffer );
		public void Write( int address, byte[] buffer ) => Device.Write( address, buffer );
		public void Dispose() => device?.Dispose();
	}
	
	/// <summary>
	/// Represents FRAM memory accessed through an I2C controller.
	/// </summary>
	/// <param name="controller">The I2C controller used to communicate with the FRAM.</param>
	/// <param name="address">The I2C address of the FRAM device.</param>
	internal sealed class FramMemory( I2cController controller, int address ) : IRawMemory
	{
		private readonly I2cController controller = controller;
		private readonly int address = address;
		private Mb85Rc256v device = null!;

		public int Capacity => Mb85Rc256v.CapacityBytes;
		private Mb85Rc256v Device => device ??= new Mb85Rc256v( controller, address );
		public void Read( int address, byte[] buffer ) => Device.Read( address, buffer );
		public void Write( int address, byte[] buffer ) => Device.Write( address, buffer );
		public void Dispose() => device?.Dispose();
	}
}
