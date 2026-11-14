using System.Threading;

namespace THERadioReceiver
{
	internal class Program
	{
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