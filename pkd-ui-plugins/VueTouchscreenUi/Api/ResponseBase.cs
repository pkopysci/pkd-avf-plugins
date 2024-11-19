namespace CrComLibUi.Api
{
	using Newtonsoft.Json;
	using System.ComponentModel;
	using System.Dynamic;

	[JsonObject(MemberSerialization.OptIn)]
	internal class ResponseBase
	{
		[DefaultValue("GET")]
		[JsonProperty("Method", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Method { get; set; } = string.Empty;

		[DefaultValue("CONFIG")]
		[JsonProperty("Command", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Command { get; set; } = string.Empty;

		[JsonProperty("Data")]
		public dynamic Data { get; set; } = new ExpandoObject();
	}
}
