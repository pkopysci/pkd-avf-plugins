using System.Collections.ObjectModel;
using pkd_application_service.VideoWallControl;

namespace CrComLibUi.Components.VideoWallControl;

public class VideoWallCellData
{
    public string Id { get; set; } = string.Empty;
    public int XPosition { get; set; }
    public int YPosition { get; set; }
    public string SourceId { get; set; } = string.Empty;
}

public class VideoWallLayoutData
{
    public string VideoWallControlId { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<VideoWallCellData> Cells { get; set; } = [];
}

public class VideoWallControlData
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<VideoWallLayoutData> Layouts { get; set; } = [];
}

public static class ConfigDataFactory
{
    public static List<VideoWallControlData> CreateControllerCollection(
        ReadOnlyCollection<VideoWallInfoContainer> videoWalls)
    {
        List<VideoWallControlData> controllerCollection = [];
        foreach (var wall in videoWalls)
        {
            controllerCollection.Add(new VideoWallControlData()
            {
                Id = wall.Id,
                Label = wall.Label,
                Icon = wall.Icon,
                Tags = wall.Tags,
                Layouts = CreateVideoWallLayoutData(wall.Layouts),
                IsOnline = wall.IsOnline,
            });
        }
        
        return controllerCollection;
    }

    private static List<VideoWallCellData> CreateVideoWallCellData(List<VideoWallCellInfo> cells)
    {
        List<VideoWallCellData> cellData = [];

        foreach (var cell in cells)
        {
            cellData.Add(new VideoWallCellData()
            {
                Id = cell.Id,
                XPosition = cell.XPosition,
                YPosition = cell.YPosition,
                SourceId = cell.SourceId,
            });
        }
        
        return cellData;
    }

    private static List<VideoWallLayoutData> CreateVideoWallLayoutData(ReadOnlyCollection<VideoWallLayoutInfo> layouts)
    {
        List<VideoWallLayoutData> layoutData = [];

        foreach (var layout in layouts)
        {
            layoutData.Add(new VideoWallLayoutData()
            {
                Id = layout.Id,
                Label = layout.Label,
                Cells = CreateVideoWallCellData(layout.Cells),
                Height = layout.Height,
                Width = layout.Width,
                VideoWallControlId = layout.VideoWallControlId,
                IsActive = false
            });
        }

        return layoutData;
    }
}