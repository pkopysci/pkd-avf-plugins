namespace CrComLibUi.Components.ErrorReporting;

using System.ComponentModel;
using Newtonsoft.Json;

[JsonObject(MemberSerialization.OptIn)]
internal class ErrorData
{
	[DefaultValue("NOTSET")]
	[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Id { get; set; } = string.Empty;

	[DefaultValue("NOTSET")]
	[JsonProperty("Message", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Message { get; set; } = string.Empty;
}