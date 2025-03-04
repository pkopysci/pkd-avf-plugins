namespace AvSwitchEmulator
{
	internal class AudioChannel
	{
		public string Id { get; set; } = string.Empty;
		public string LevelTag { get; set; } = string.Empty;
		public string MuteTag { get; set; } = string.Empty;
		public string RouterTag { get; set; } = string.Empty;
		public int RouterIndex { get; set; }
		public int LevelMin { get; set; }
		public int LevelMax { get; set; }
		public int CurrentLevel { get; set; }
		public int BankIndex {  get; set; }
		public bool MuteState { get; set; }
	}
}
