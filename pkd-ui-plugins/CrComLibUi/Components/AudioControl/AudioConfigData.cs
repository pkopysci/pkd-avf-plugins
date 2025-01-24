namespace CrComLibUi.Components.AudioControl;

using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

[JsonObject(MemberSerialization.OptIn)]
internal class Dsp
{
    [DefaultValue("DSP-ID-NOTSET")]
    [JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Id { get; set; } = string.Empty;

    [DefaultValue("Blank Manufacturer")]
    [JsonProperty("Manufacturer", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Manufacturer { get; set; } = string.Empty;

    [DefaultValue("Blank Model")]
    [JsonProperty("Model", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("IsOnline")] public bool IsOnline { get; set; }
}

internal class EnableZone
{
    [DefaultValue("VSOURCE-ID-NOTSET")]
    [JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Id { get; set; } = string.Empty;

    [DefaultValue("Display Input")]
    [JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("Enabled")] public bool Enabled { get; set; }
}

internal class AudioChannel
{
    [DefaultValue("VSOURCE-ID-NOTSET")]
    [JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Id { get; set; } = string.Empty;

    [DefaultValue("Display Input")]
    [JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Label { get; set; } = string.Empty;

    [DefaultValue("alert")]
    [JsonProperty("Icon", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Icon { get; set; } = string.Empty;

    [JsonProperty("HasSync")] public bool HasSync { get; set; }

    [JsonProperty("MuteState")] public bool MuteState { get; set; }

    [JsonProperty("Level")] public int Level { get; set; }

    [JsonProperty("Tags")] public List<string> Tags { get; set; } = [];

    [JsonProperty("Zones")] public List<EnableZone> Zones { get; set; } = [];

    [DefaultValue("")]
    [JsonProperty("RoutedInput", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string RoutedInput { get; set; } = string.Empty;
}

internal class AudioConfigData
{
    [JsonProperty("Dsp")] public Dsp Dsp { get; set; } = new Dsp();

    [JsonProperty("Inputs")] public List<AudioChannel> Inputs { get; set; } = [];

    [JsonProperty("Outputs")] public List<AudioChannel> Outputs { get; set; } = [];
}