﻿
namespace CrComLibUi.Api;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Dynamic;

[JsonObject(MemberSerialization.OptIn)]
internal class ResponseBase
{
	[DefaultValue("GET")]
	[JsonProperty(nameof(Method), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Method { get; set; } = string.Empty;

	[DefaultValue("CONFIG")]
	[JsonProperty(nameof(Command), DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Command { get; set; } = string.Empty;

	[JsonProperty(nameof(Data))]
	public JObject Data { get; set; } = new();
}