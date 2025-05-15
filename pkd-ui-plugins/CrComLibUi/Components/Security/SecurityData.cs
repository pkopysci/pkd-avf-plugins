namespace CrComLibUi.Components.Security;

using System.ComponentModel;
using Newtonsoft.Json;

internal class SecurityData
{
	[DefaultValue("")]
	[JsonProperty("Code", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Code { get; set; } = string.Empty;

	public bool Result { get; set; }
}