namespace CrComLibUi.Components.RoomInfo;

using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

[JsonObject(MemberSerialization.OptIn)]
internal class MainMenuItem
{
	[DefaultValue("MENUITEMID")]
	[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = string.Empty;

	[DefaultValue("Source Label")]
	[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Label { get; set; } = string.Empty;

	[DefaultValue("hdmi")]
	[JsonProperty("Icon", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Icon { get; set; } = string.Empty;

	[DefaultValue("av-routing")]
	[JsonProperty("Control", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Control { get; set; } = string.Empty;

	[DefaultValue("")]
	[JsonProperty("Source", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Source { get; set; } = string.Empty;

	[JsonProperty("Tags")] public List<string> Tags { get; set; } = [];
}


[JsonObject(MemberSerialization.OptIn)]
internal class RoomConfigData
{
	[DefaultValue(false)]
	[JsonProperty("IsInUse", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool IsInUse { get; set; }

	[DefaultValue("NOT SET")]
	[JsonProperty("RoomName", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string RoomName { get; set; } = string.Empty;

	[DefaultValue("555-555-5555")]
	[JsonProperty("HelpNumber", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string HelpNumber { get; set; } = string.Empty;

	[DefaultValue(false)]
	[JsonProperty("IsSecure", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool IsSecure { get; set; }

	[DefaultValue(false)]
	[JsonProperty("IsTech", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool IsTech { get; set; }

	[DefaultValue("baseline")]
	[JsonProperty("RoomType", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string RoomType { get; set; } = string.Empty;

	[DefaultValue("av-routing")]
	[JsonProperty("DefaultActivity", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string DefaultActivity { get; set; } = string.Empty;

	[JsonProperty("MainMenu")] public List<MainMenuItem> MainMenu { get; set; } = [];
}