namespace CrComLibUi.Components.CustomEvents
{
	using Newtonsoft.Json;
	using System.ComponentModel;

	internal class CustomEventData
	{
		[DefaultValue("")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("")]
		[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Label { get; set; }

		[JsonProperty("State")]
		public bool State { get; set; }
	}
}
