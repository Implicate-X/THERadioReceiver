using System;
using System.Collections;
using System.Text;
using System.Threading;

namespace THERadioReceiver
{
	public sealed class Chassis
	{
		public Panel Panel { get; private set; } = null!;
		public Board Board { get; private set; } = null!;
		public FMRadio FMRadio { get; private set; } = null!;
		public WebRadio WebRadio { get; private set; } = null!;
		public Chassis()
		{
			Board = new Board( this );
			Panel = new Panel( this );
			FMRadio = new FMRadio( this );
			WebRadio = new WebRadio( this );

			Initialize();
		}

		private void Initialize()
		{
			Board.Initialize();
			Panel.Initialize();
			FMRadio.Initialize();
			WebRadio.Initialize();
		}
	}
}
