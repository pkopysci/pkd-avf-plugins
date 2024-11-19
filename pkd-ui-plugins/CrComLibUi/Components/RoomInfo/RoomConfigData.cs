namespace CrComLibUi.Components.RoomInfo
{
	using Newtonsoft.Json;
	using System.Collections.Generic;
	using System.ComponentModel;

	[JsonObject(MemberSerialization.OptIn)]
	internal class MainMenuItem
	{
		[DefaultValue("MENUITEMID")]
		[JsonProperty("Id", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Id { get; set; }

		[DefaultValue("Source Label")]
		[JsonProperty("Label", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Label { get; set; }

		[DefaultValue("hdmi")]
		[JsonProperty("Icon", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Icon { get; set; }

		[DefaultValue("av-routing")]
		[JsonProperty("Control", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Control { get; set; }

		[DefaultValue("")]
		[JsonProperty("Source", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Source { get; set; }

		[JsonProperty("Tags")]
		public List<string> Tags { get; set; } = new List<string>();
	}


	[JsonObject(MemberSerialization.OptIn)]
	internal class RoomConfigData
	{
		[DefaultValue(false)]
		[JsonProperty("IsInUse", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsInUse { get; set; }

		[DefaultValue("NOT SET")]
		[JsonProperty("RoomName", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string RoomName { get; set; }

		[DefaultValue("555-555-5555")]
		[JsonProperty("HelpNumber", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string HelpNumber { get; set; }

		[DefaultValue(false)]
		[JsonProperty("IsSecure", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsSecure { get; set; }

		[DefaultValue(false)]
		[JsonProperty("IsTech", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsTech { get; set; }

		[DefaultValue("baseline")]
		[JsonProperty("RoomType", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string RoomType { get; set; }

		[DefaultValue("av-routing")]
		[JsonProperty("DefaultActivity", DefaultValueHandling = DefaultValueHandling.Populate)]
		public string DefaultActivity { get; set; }

		[JsonProperty("MainMenu")]
		public List<MainMenuItem> MainMenu { get; set; } = new List<MainMenuItem>();
	}
}
