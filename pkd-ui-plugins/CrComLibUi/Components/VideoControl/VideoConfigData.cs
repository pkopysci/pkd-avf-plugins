namespace CrComLibUi.Components.VideoControl;

using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

[JsonObject(MemberSerialization.OptIn)]
internal class AvRouter
{
	[DefaultValue("AVR-ID-NOTSET")]
	[JsonProperty(nameof(Id), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = string.Empty;
	
	[DefaultValue("Not Set")]
	[JsonProperty(nameof(Model), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Model { get; set; } = string.Empty;

	[DefaultValue("Default AVR Label")]
	[JsonProperty(nameof(Label), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Label { get; set; } = string.Empty;

	[DefaultValue(false)]
	[JsonProperty(nameof(IsOnline), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool IsOnline { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
internal class VideoSource
{
	[DefaultValue("VSOURCE-ID-NOTSET")]
	[JsonProperty(nameof(Id), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = string.Empty;

	[DefaultValue("Video Source Label")]
	[JsonProperty(nameof(Label), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Label { get; set; } = string.Empty;

	[DefaultValue("alert")]
	[JsonProperty(nameof(Icon), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Icon { get; set; } = string.Empty;

	[DefaultValue("")]
	[JsonProperty(nameof(Control), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Control { get; set; } = string.Empty;
    
	[JsonProperty(nameof(SupportsSync))]
    public bool SupportsSync {  get; set; }

	[DefaultValue("false")]
	[JsonProperty(nameof(HasSync), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool HasSync { get; set; }

	[JsonProperty(nameof(Tags))] public List<string> Tags { get; set; } = [];
}

[JsonObject(MemberSerialization.OptIn)]
internal class VideoDestination
{
	[DefaultValue("VSOURCE-ID-NOTSET")]
	[JsonProperty(nameof(Id), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = string.Empty;

	[DefaultValue("Video Source Label")]
	[JsonProperty(nameof(Label), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Label { get; set; } = string.Empty;

	[DefaultValue("alert")]
	[JsonProperty(nameof(Icon), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Icon { get; set; } = string.Empty;

	[DefaultValue("")]
	[JsonProperty(nameof(CurrentSourceId), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string CurrentSourceId { get; set; } = string.Empty;

	[JsonProperty(nameof(Tags))] public List<string> Tags { get; set; } = [];
}

[JsonObject(MemberSerialization.OptIn)]
internal class DisplayInput
{
	[DefaultValue("VSOURCE-ID-NOTSET")]
	[JsonProperty(nameof(Id), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = string.Empty;

	[DefaultValue("Display Input")]
	[JsonProperty(nameof(Label), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Label { get; set; } = string.Empty;

	[DefaultValue(false)]
	[JsonProperty(nameof(Selected), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Selected { get; set; }

	[JsonProperty(nameof(Tags))] public List<string> Tags { get; set; } = [];
}

[JsonObject(MemberSerialization.OptIn)]
internal class Display
{
	[DefaultValue("Unknown")]
	[JsonProperty(nameof(Manufacturer), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Manufacturer { get; set; } = string.Empty;
	
	[DefaultValue("Unknown")]
	[JsonProperty(nameof(Model), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Model { get; set; } = string.Empty;
	
	[DefaultValue("VSOURCE-ID-NOTSET")]
	[JsonProperty(nameof(Id), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = string.Empty;

	[DefaultValue("Display Input")]
	[JsonProperty(nameof(Label), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Label { get; set; } = string.Empty;

	[DefaultValue("alert")]
	[JsonProperty(nameof(Icon), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Icon { get; set; } = string.Empty;

	[DefaultValue(false)]
	[JsonProperty(nameof(IsOnline), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool IsOnline { get; set; }

	[DefaultValue(false)]
	[JsonProperty(nameof(HasScreen), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool HasScreen { get; set; }

	[DefaultValue(false)]
	[JsonProperty(nameof(PowerState), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool PowerState { get; set; }

	[JsonProperty(nameof(Tags))] public List<string> Tags { get; set; } = [];

	[JsonProperty(nameof(Inputs))] public List<DisplayInput> Inputs { get; set; } = [];

	[JsonProperty(nameof(Blank))]
	public bool Blank { get; set; }

	[JsonProperty(nameof(Freeze))]
	public bool Freeze { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
internal class VideoConfigData
{
	[DefaultValue(false)]
	[JsonProperty(nameof(Blank), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Blank { get; set; }

	[DefaultValue(false)]
	[JsonProperty(nameof(Freeze), DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Freeze { get; set; }

	[JsonProperty(nameof(AvRouters))] public List<AvRouter> AvRouters { get; set; } = [];

	[JsonProperty(nameof(Sources))] public List<VideoSource> Sources { get; set; } = [];

	[JsonProperty(nameof(Destinations))] public List<VideoDestination> Destinations { get; set; } = [];
}