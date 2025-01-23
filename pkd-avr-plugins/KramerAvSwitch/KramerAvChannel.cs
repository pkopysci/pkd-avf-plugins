namespace KramerAvSwitch
{
	/// <summary>
	/// Helper class for the KramerAvSwitch. Used to store channel states.
	/// </summary>
	internal class KramerAvChannel
	{
		/// <summary>
		/// Gets or sets the unique ID of the AV channel.
		/// </summary>
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the physical input or output index for this channel.
		/// </summary>
		public uint Number { get; init; }

		/// <summary>
		/// Gets or sets a value indicating whether the video freeze is on or off.
		/// </summary>
		public bool VideoFreeze { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the video mute is on or off.
		/// </summary>
		public bool VideoMute { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the audio mute is on or off.
		/// </summary>
		public bool AudioMute { get; set; }

		/// <summary>
		/// Gets or sets the audio level as of the last response.
		/// </summary>
		public int AudioLevel { get; set; }

		/// <summary>
		/// Gets or sets the audio level that the channels should return to when not muted.
		/// </summary>
		public int UnmutedLevel { get; set; }

		/// <summary>
		/// Gets or sets the input routed as of the last response.
		/// </summary>
		public uint CurrentSource { get; set; }
	}
}
