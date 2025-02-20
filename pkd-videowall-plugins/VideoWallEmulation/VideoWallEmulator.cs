using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_domain_service.Data.RoutingData;
using pkd_hardware_service.VideoWallDevices;

namespace VideoWallEmulation;

public class VideoWallEmulator : IVideoWallDevice
{
    private VideoWallLayout? _activeLayout;
    
    public event EventHandler? VideoWallLayoutChanged;
    public event EventHandler<GenericSingleEventArgs<string>>? VideoWallCellSourceChanged;
    public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

    public string Id { get; private set; } = "DefaultId";
    public string Label { get; private set; } = string.Empty;
    
    public string StartupLayoutId { get; } = "vw01";
    public bool IsOnline { get; private set; }
    public bool IsInitialized { get; private set; }
    public List<VideoWallLayout> Layouts { get; private set; } = [];
    public List<Source> Sources { get; private set; } = [];
    public int MaxHeight { get; } = 2;
    public int MaxWidth { get; } = 4;
    
    
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
        Layouts = WallData.CreateLayouts();
        Sources = WallData.CreateSources();
        IsInitialized = true;
    }

    public void SetActiveLayout(string id)
    {
        var found = Layouts.Find(x => x.Id == id);
        if (found == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - SetActiveLayout() - Layout {id} not found.");
            return;
        }
        
        _activeLayout = found;
        var temp = VideoWallLayoutChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));

        foreach (var cell in _activeLayout.Cells)
        {
            SetCellSource(cell.Id, cell.DefaultSourceId);
        }
    }

    public string GetActiveLayoutId()
    {
        return _activeLayout?.Id ?? string.Empty;
    }

    public void SetCellSource(string cellId, string sourceId)
    {
        if (_activeLayout == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - SetCellSource() - No active layout.");
            return;
        }
        
        var cell = _activeLayout.Cells.Find(x => x.Id == cellId);
        if (cell == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - SetCellSource() - Layout {_activeLayout.Id} does not have a cell with id {cellId}.");
            return;
        }
        
        cell.SourceId = sourceId;
        var temp = VideoWallCellSourceChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(cell.Id));
    }

    public string GetCellSourceId(string cellId)
    {
        if (_activeLayout == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - GetCellSourceId() - No active layout.");
            return string.Empty;
        }
        
        var cell = _activeLayout.Cells.Find(x => x.Id == cellId);
        if (cell == null)
        {
            Logger.Error($"VideoWallEmulator {Id} - GetCellSourceId() - Layout {_activeLayout.Id} does not have a cell with id {cellId}.");
            return string.Empty;
        }
        
        return cell.SourceId;
    }
}