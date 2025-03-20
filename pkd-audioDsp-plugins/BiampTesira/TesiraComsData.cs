namespace BiampTesira;

public class TesiraComsData
{
    public string ChannelId { get; set; } = string.Empty;
    public string InstanceTag { get; set; } = string.Empty;
    public string SerializedCommand { get; set; } = string.Empty;
    public BlockTypes BlockType { get; set; } = BlockTypes.NotSet;
}