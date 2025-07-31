using System.Collections.ObjectModel;
using pkd_application_service.AvRouting;
using pkd_application_service.VideoWallControl;
using pkd_common_utils.Logging;

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

    public void SetCellSource(string cellId, string sourceId)
    {
        var cell = Cells.FirstOrDefault(x => x.Id.Equals(cellId));
        if (cell != null) cell.SourceId = sourceId;
    }
}

public class VideoWallCanvasData
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string StartupLayoutId { get; init; } = string.Empty;
    
    public string ActiveLayoutId { get; set; } = string.Empty;
    public int MaxWidth { get; init; }
    public int MaxHeight { get; init; }
    public List<VideoWallLayoutData> Layouts { get; init; } = [];

    public VideoWallLayoutData? GetActiveLayout()
    {
        return Layouts.FirstOrDefault(x => x.Id.Equals(ActiveLayoutId));
    }
}

public class VideoWallControlData
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public bool IsOnline { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<VideoWallCanvasData> Canvases { get; set; } = [];
    
    public List<AvSourceInfoContainer> Sources { get; init; } = [];
}

public static class ConfigDataFactory
{
    public static List<VideoWallControlData> CreateControllerCollection(
        ReadOnlyCollection<VideoWallInfoContainer> videoWalls)
    {
        List<VideoWallControlData> controllerCollection = [];
        
        foreach (var wall in videoWalls)
        {

            Logger.Debug("CrComLibUI.VideoWallControl.ConfigDataFactory.CreateControllerCollection()");
            Logger.Debug($"Number of sources for controller {wall.Id}: {wall.Sources.Count}");
            foreach (var sources in wall.Sources)
            {
                Logger.Debug($"{sources.Id} - {sources.Label}");
            }
            
            controllerCollection.Add(new VideoWallControlData()
            {
                Id = wall.Id,
                Model = wall.Model,
                Label = wall.Label,
                Icon = wall.Icon,
                Tags = wall.Tags,
                Canvases = CreateVideoWallCanvasInfos(wall),
                IsOnline = wall.IsOnline,
                Sources = wall.Sources.ToList(),
            });
        }
        
        return controllerCollection;
    }

    private static List<VideoWallCanvasData> CreateVideoWallCanvasInfos(
        VideoWallInfoContainer videoWall)
    {
        List<VideoWallCanvasData> canvases = [];

        foreach (var canvas in videoWall.Canvases)
        {
            canvases.Add(new()
            {
                Id = canvas.Id,
                Label = canvas.Label,
                StartupLayoutId = canvas.StartupLayoutId,
                MaxWidth = canvas.MaxWidth,
                MaxHeight = canvas.MaxHeight,
                Layouts = CreateVideoWallLayoutData(canvas.Layouts)
            });
        }
        
        return canvases;
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

    private static List<VideoWallLayoutData> CreateVideoWallLayoutData(List<VideoWallLayoutInfo> layouts)
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