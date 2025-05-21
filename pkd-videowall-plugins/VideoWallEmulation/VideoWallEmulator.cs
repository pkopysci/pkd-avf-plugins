using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_domain_service.Data.RoutingData;
using pkd_hardware_service.VideoWallDevices;

namespace VideoWallEmulation;

public class VideoWallEmulator : IVideoWallDevice
{
    private List<EmulatedVideoWallCanvas> _internalCanvases = [];
    
    public event EventHandler<GenericSingleEventArgs<string>>? VideoWallLayoutChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? VideoWallCellSourceChanged;
    public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

    public string Id { get; private set; } = "DefaultId";
    public string Label { get; private set; } = string.Empty;
    public string Manufacturer { get; set; } = "Emulation Inc.";
    public string Model { get; set; } = "VW Emulator 9001";
    
    public bool IsOnline { get; private set; }
    public bool IsInitialized { get; private set; }

    public List<Source> Sources { get; private set; } = [];
    
    public List<VideoWallCanvas> Canvases
    {
        get
        {
            var canvases = new List<VideoWallCanvas>();
            foreach (var internalCanvas in _internalCanvases)
            {
                canvases.Add(internalCanvas);
            }
            
            return canvases;
        }    
    }
    
    
    public void Connect()
    {
        IsOnline = true;
        var temp = ConnectionChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
    }

    public void Disconnect()
    {
        IsOnline = false;
        var temp = ConnectionChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
    }
    
    public void Initialize(string hostname, int port, string id, string label, string username, string password)
    {
        Id = id;
        Label = label;
        _internalCanvases = WallData.CreateCanvases();
        Sources = WallData.CreateSources();
        IsInitialized = true;
    }

    public void SetActiveLayout(string canvasId, string layoutId)
    {
        var canvas = _internalCanvases.Find(x => x.Id == canvasId);
        if (canvas == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - SetActiveLayout() - canvas {canvasId} not found.");
            return;
        }

        var layout = canvas.Layouts.FirstOrDefault(y => y.Id.Equals(layoutId));
        if (layout == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - SetActiveLayout() - canvas {canvasId} does not have a layout with id {layoutId}.");
            return;
        }
        
        canvas.ActiveLayoutId = layoutId;

        var temp = VideoWallLayoutChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(canvas.Id));

        foreach (var cell in layout.Cells)
        {
            SetCellSource(canvasId, cell.Id, cell.DefaultSourceId);
        }
    }

    public string GetActiveLayoutId(string canvasId)
    {
        var canvas = _internalCanvases.FirstOrDefault(x => x.Id.Equals(canvasId));
        if (canvas == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - GetActiveLayoutId() - canvas {canvasId} not found.");
            return string.Empty;
        }
        
        return canvas.ActiveLayoutId;
    }

    public void SetCellSource(string canvasId, string cellId, string sourceId)
    {
        var canvas = _internalCanvases.FirstOrDefault(x => x.Id.Equals(canvasId));
        if (canvas == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - SetCellSource() - canvas {canvasId} not found.");
            return;
        }

        if (!canvas.TryGetVideoWallCell(cellId, out var cell))
        {
            Logger.Error($"VideoWallEmulator {Id} - SetCellSource() - active layout for {canvasId} does not have a cell with id {cellId}.");
            return;
        }
        
        if (cell != null) cell.SourceId = sourceId;
        var temp = VideoWallCellSourceChanged;
        temp?.Invoke(this, new GenericDualEventArgs<string,string>(canvasId, cell?.Id ?? string.Empty));
    }

    public string GetCellSourceId(string canvasId, string cellId)
    {
        var canvas = _internalCanvases.FirstOrDefault(x => x.Id.Equals(canvasId));
        if (canvas == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - GetCellSourceId() - canvas {canvasId} not found.");
            return string.Empty;
        }
        
        if (!canvas.TryGetVideoWallCell(cellId, out var cell))
        {
            Logger.Error($"VideoWallEmulator {Id} - GetCellSourceId() - active layout for {canvasId} does not have a cell with id {cellId}.");
            return string.Empty;
        }
        
        return (cell == null) ? string.Empty : cell.SourceId;
    }
}