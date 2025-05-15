namespace CrComLibUi.Components.CustomEvents;

using System.ComponentModel;
using Newtonsoft.Json;

internal class CustomEventData
{
	[DefaultValue("")]
	[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = string.Empty;

	[DefaultValue("")]
	[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Label { get; set; } = string.Empty;

	[JsonProperty("State")]
	public bool State { get; set; }
}