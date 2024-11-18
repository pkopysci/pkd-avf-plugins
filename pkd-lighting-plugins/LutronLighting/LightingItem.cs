namespace LutronLighting
{
	internal class LightingItem
	{
		public string Id { get; set; }
		public int Index { get; set; }
		public string Label { get; set; }
	}

	internal class LightingZoneItem : LightingItem
	{
		public int Level { get; set; }
	}
}
