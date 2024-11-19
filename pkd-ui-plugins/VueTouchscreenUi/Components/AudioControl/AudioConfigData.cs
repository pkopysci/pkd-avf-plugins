namespace CrComLibUi.Components.AudioControl
{
	using Newtonsoft.Json;
	using System.Collections.Generic;
	using System.ComponentModel;

	[JsonObject(MemberSerialization.OptIn)]
	internal class Dsp
	{
		[DefaultValue("DSP-ID-NOTSET")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("Blank Manufacturer")]
		[JsonProperty("Manufacturer", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Manufacturer { get; set; }

		[DefaultValue("Blank Model")]
		[JsonProperty("Model", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Model { get; set; }

		[JsonProperty("IsOnline")]
		public bool IsOnline { get; set; }
	}

	internal class EnableZone
	{
		[DefaultValue("VSOURCE-ID-NOTSET")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("Display Input")]
		[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Label { get; set; }

		[JsonProperty("Enabled")]
		public bool Enabled { get; set; }
	}

	internal class AudioChannel
	{
		[DefaultValue("VSOURCE-ID-NOTSET")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("Display Input")]
		[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Label { get; set; }

		[DefaultValue("alert")]
		[JsonProperty("Icon", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Icon { get; set; }

		[JsonProperty("HasSync")]
		public bool HasSync { get; set; }

		[JsonProperty("MuteState")]
		public bool MuteState { get; set; }

		[JsonProperty("Level")]
		public int Level { get; set; }

		[JsonProperty("Tags")]
		public List<string> Tags { get; set; } = new List<string>();

		[JsonProperty("Zones")]
		public List<EnableZone> Zones { get; set; } = new List<EnableZone>();

		[DefaultValue("")]
		[JsonProperty("RoutedInput", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string RoutedInput { get; set; }
	}

	internal class AudioConfigData
	{
		[JsonProperty("Dsp")]
		public Dsp Dsp { get; set; }

		[JsonProperty("Inputs")]
		public List<AudioChannel> Inputs { get; set; } = new List<AudioChannel>();

		[JsonProperty("Outputs")]
		public List<AudioChannel> Outputs { get; set; } = new List<AudioChannel>();
	}
}
