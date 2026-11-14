using System.Threading;

namespace THERadioReceiver
{
	/// <summary>
	/// Provides the application's entry point.
	/// </summary>
	internal class Program
	{
		/// <summary>
		/// Initializes the chassis and keeps the application running.
		/// </summary>
		private static void Main()
		{
			_ = new Chassis();

			while( true )
			{
				Thread.Sleep( 1000 );
			}
		}
	}
}