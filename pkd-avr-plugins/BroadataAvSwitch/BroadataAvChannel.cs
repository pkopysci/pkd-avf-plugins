namespace BroadataAvSwitch
{
	internal class BroadataAvChannel
	{
		public string Id { get; set; }
		public int Number { get; set; }
		public bool VideoFreeze { get; set; }
		public bool VideoMute { get; set; }
		public bool AudioMute { get; set; }
		public int AudioLevel { get; set; }
		public uint CurrentSource { get; set; }
		public int PreMuteLevel { get; set; }
	}
}
