namespace CrComLibUi.Components.Lighting
{
	using System.Collections.Generic;

	internal class LightingItem
	{
		public string Id { get; set; }

		public string Label { get; set; }

		public List<string> Tags { get; set; } = new List<string>();
	}

	internal class LightingSceneData : LightingItem
	{
		public bool Set { get; set; }
	}

	internal class LightingZoneData : LightingItem
	{
		public int Load { get; set; }
	}

	internal class LightingData : LightingItem
	{
		public List<LightingSceneData> Scenes { get; set; } = new List<LightingSceneData>();

		public List<LightingZoneData> Zones { get; set; } = new List<LightingZoneData>();
		
		public string SelectedSceneId { get; set; } = string.Empty;
	}
}
