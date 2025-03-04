namespace BroadataAvSwitch
{
	internal class BroadataAvChannel
	{
		public string Id { get; set; } = string.Empty;
		public bool AudioMute { get; set; }
		public int AudioLevel { get; set; }
		public uint CurrentSource { get; set; }
		public int PreMuteLevel { get; set; }
	}
}
