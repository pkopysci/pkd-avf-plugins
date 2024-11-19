namespace CrComLibUi.Components.ErrorReporting
{
	using Newtonsoft.Json;
	using System.ComponentModel;

	[JsonObject(MemberSerialization.OptIn)]
	internal class ErrorData
	{
		[DefaultValue("NOTSET")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("NOTSET")]
		[JsonProperty("Message", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Message { get; set; }
	}
}
