namespace THERadioReceiver
{
	/// <summary>
	/// Creates and initializes the components that make up the radio receiver chassis.
	/// </summary>
	public sealed class Chassis
	{
		/// <summary>
		/// Gets the receiver's main board.
		/// </summary>
		public Board Board { get; private set; } = null!;

		/// <summary>
		/// Gets the receiver's control front panel.
		/// </summary>
		public Panel Panel { get; private set; } = null!;

		/// <summary>
		/// Gets the FM radio component.
		/// </summary>
		public FmRadio FMRadio { get; private set; } = null!;

		/// <summary>
		/// Gets the web radio component.
		/// </summary>
		public WebRadio WebRadio { get; private set; } = null!;

		/// <summary>
		/// Gets the audio system component.
		/// </summary>
		public AudioSystem AudioSystem { get; private set; } = null!;

		/// <summary>
		/// Creates the chassis components and initializes them.
		/// </summary>
		public Chassis()
		{
			Board = new();
			Panel = new( this );
			FMRadio = new( this );
			WebRadio = new( this );
			AudioSystem = new( this );

			Initialize();
		}

		/// <summary>
		/// Initializes each chassis component.
		/// </summary>
		private void Initialize()
		{
			Board.Initialize();
			Panel.Initialize();
			FMRadio.Initialize();
			WebRadio.Initialize();
			AudioSystem.Initialize();
		}
	}
}