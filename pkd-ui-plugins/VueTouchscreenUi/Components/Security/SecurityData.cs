namespace CrComLibUi.Components.Security
{
	using Newtonsoft.Json;
	using System.ComponentModel;

	internal class SecurityData
	{
		[DefaultValue("")]
		[JsonProperty("Code", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Code { get; set; }

		public bool Result { get; set; }
	}
}
