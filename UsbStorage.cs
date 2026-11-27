using System.IO;
using System.Diagnostics;
using GHIElectronics.TinyCLR.Devices.Storage;
using GHIElectronics.TinyCLR.Devices.UsbHost;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.IO;

namespace THERadioReceiver
{
	/// <summary>
	/// Manages USB storage connections and mounts connected mass-storage devices.
	/// </summary>
	/// <remarks>Enables the default USB host controller and enumerates files on the mounted drive when a
	/// mass-storage device connects.</remarks>
	public class UsbStorage
	{
		/// <summary>
		/// The default USB host controller.
		/// </summary>
		private readonly UsbHostController usbHostController = UsbHostController.GetDefault();

		/// <summary>
		/// Initializes USB storage monitoring.
		/// </summary>
		public UsbStorage()
		{
			usbHostController.OnConnectionChangedEvent += UsbHostController_OnConnectionChangedEvent;
			usbHostController.Enable();

		}

		/// <summary>
		/// Handles a USB device connection status change.
		/// </summary>
		/// <param name="sender">The USB host controller that raised the event.</param>
		/// <param name="e">The event data containing the device connection status.</param>
		private void UsbHostController_OnConnectionChangedEvent( UsbHostController sender, DeviceConnectionEventArgs e )
		{
			switch( e.DeviceStatus )
			{
				case DeviceConnectionStatus.Connected:
					HandleConnect( e );
					break;
				case DeviceConnectionStatus.Disconnected:
					break;
				case DeviceConnectionStatus.Bad:
					break;
			}
		}

		/// <summary>
		/// Handles a device connection event and mounts a mass-storage device.
		/// </summary>
		/// <param name="e">The device connection event arguments.</param>
		void HandleConnect( DeviceConnectionEventArgs e )
		{
			switch( e.Type )
			{
				case BaseDevice.DeviceType.MassStorage:
					MountMassStorage();

					FileEnumerator files = new( @"A:\\", FileEnumFlags.Files );

					foreach( var file in files )
					{
						Debug.WriteLine( "Found file: " + file );

					}
					break;
			}
		}

		/// <summary>
		/// Mounts a mass-storage device.
		/// </summary>
		void MountMassStorage()
		{
			var storage = StorageController.FromName( SC20100.StorageController.UsbHostMassStorage );
			var drive = FileSystem.Mount( storage.Hdc );
			var info = new DriveInfo( drive.Name );

			Debug.WriteLine( "Volume: " + info.VolumeLabel );
			Debug.WriteLine( "Format: " + info.DriveFormat );
			Debug.WriteLine( "Total size: " + info.TotalSize );
			Debug.WriteLine( "Free space: " + info.TotalFreeSpace );
			Debug.WriteLine( "Root: " + info.RootDirectory );
		}
	}
}
