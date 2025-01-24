namespace LutronLighting;

internal class LightingItem
{
	public string Id { get; init; } = string.Empty;
	public int Index { get; init; }
	public string Label { get; set; } = string.Empty;
}

internal class LightingZoneItem : LightingItem
{
	public int Level { get; set; }
}