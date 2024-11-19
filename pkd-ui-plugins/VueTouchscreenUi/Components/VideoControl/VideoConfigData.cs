namespace CrComLibUi.Components.VideoControl
{
	using Newtonsoft.Json;
	using System.Collections.Generic;
	using System.ComponentModel;

	[JsonObject(MemberSerialization.OptIn)]
	internal class AvRouter
	{
		[DefaultValue("AVR-ID-NOTSET")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("Default AVR Label")]
		[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Label { get; set; }

		[DefaultValue(false)]
		[JsonProperty("IsOnline", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsOnline { get; set; }
	}

	[JsonObject(MemberSerialization.OptIn)]
	internal class VideoSource
	{
		[DefaultValue("VSOURCE-ID-NOTSET")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("Video Source Label")]
		[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Label { get; set; }

		[DefaultValue("alert")]
		[JsonProperty("Icon", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Icon { get; set; }

		[DefaultValue("")]
		[JsonProperty("Control", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Control { get; set; }

		[DefaultValue("false")]
		[JsonProperty("HasSync", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasSync { get; set; }

		[JsonProperty("Tags")]
		public List<string> Tags { get; set; } = new List<string>();
	}

	[JsonObject(MemberSerialization.OptIn)]
	internal class VideoDestination
	{
		[DefaultValue("VSOURCE-ID-NOTSET")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("Video Source Label")]
		[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Label { get; set; }

		[DefaultValue("alert")]
		[JsonProperty("Icon", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Icon { get; set; }

		[DefaultValue("")]
		[JsonProperty("CurrentSourceId", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string CurrentSourceId { get; set; }

		[JsonProperty("Tags")]
		public List<string> Tags { get; set; } = new List<string>();
	}

	[JsonObject(MemberSerialization.OptIn)]
	internal class DisplayInput
	{
		[DefaultValue("VSOURCE-ID-NOTSET")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("Display Input")]
		[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Label { get; set; }

		[DefaultValue(false)]
		[JsonProperty("Selected", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Selected { get; set; }

		[JsonProperty("Tags")]
		public List<string> Tags { get; set; } = new List<string>();
	}

	[JsonObject(MemberSerialization.OptIn)]
	internal class Display
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

		[DefaultValue(false)]
		[JsonProperty("IsOnline", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsOnline { get; set; }

		[DefaultValue(false)]
		[JsonProperty("HasScreen", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasScreen { get; set; }

		[DefaultValue(false)]
		[JsonProperty("PowerState", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool PowerState { get; set; }

		[JsonProperty("Tags")]
		public List<string> Tags { get; set; } = new List<string>();

		[JsonProperty("Inputs")]
		public List<DisplayInput> Inputs { get; set; } = new List<DisplayInput>();

		[JsonProperty("Blank")]
		public bool Blank { get; set; }

		[JsonProperty("Freeze")]
		public bool Freeze { get; set; }
	}

	[JsonObject(MemberSerialization.OptIn)]
	internal class VideoConfigData
	{
		[DefaultValue(false)]
		[JsonProperty("Blank", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Blank { get; set; }

		[DefaultValue(false)]
		[JsonProperty("Freeze", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool Freeze { get; set; }

		[JsonProperty("AvRouters")]
		public List<AvRouter> AvRouters { get; set; } = new List<AvRouter>();

		[JsonProperty("Sources")]
		public List<VideoSource> Sources { get; set; } = new List<VideoSource>();

		[JsonProperty("Destinations")]
		public List<VideoDestination> Destinations { get; set; } = new List<VideoDestination>();
	}
}
