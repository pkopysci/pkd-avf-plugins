namespace CrComLibUi.Components.Lighting;

using System.Collections.Generic;

internal class LightingItem
{
	public string Id { get; set; } = string.Empty;

	public string Label { get; set; } = string.Empty;

	public List<string> Tags { get; set; } = [];
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
	public string Model { get; set; } = string.Empty;
	
	public List<LightingSceneData> Scenes { get; init; } = [];

	public List<LightingZoneData> Zones { get; init; } = [];
		
	public string SelectedSceneId { get; set; } = string.Empty;
	
	public bool IsOnline { get; set; }
}