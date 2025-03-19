namespace BiampTesira;

internal class TesiraChannel
{
    public string Id { get; init; } = string.Empty;
    public int Index { get; init; }
    public int LevelMin { get; init; }
    public int LevelMax { get; init; }
    
    public int Level { get; set; }
    public bool Mute { get; set; }
    public string Route { get; set; } = string.Empty;
}