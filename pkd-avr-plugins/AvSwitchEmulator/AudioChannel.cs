namespace AvSwitchEmulator
{
	internal class AudioChannel
	{
		public string Id { get; set; }
		public string LevelTag { get; set; }
		public string MuteTag { get; set; }
		public string RouterTag { get; set; }
		public int RouterIndex { get; set; }
		public int LevelMin { get; set; }
		public int LevelMax { get; set; }
		public int CurrentLevel { get; set; }
		public int BankIndex {  get; set; }

		public bool MuteState { get; set; }
	}
}
