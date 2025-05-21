namespace VideoWallTestingApp;

using System.Collections.ObjectModel;
using pkd_application_service;
using pkd_application_service.AvRouting;
using pkd_application_service.VideoWallControl;
using pkd_common_utils.GenericEventArgs;
using pkd_domain_service;
using pkd_hardware_service;
using pkd_hardware_service.VideoWallDevices;

public class VideoWallApp : ApplicationService, IVideoWallApp
{
    public event EventHandler<GenericDualEventArgs<string, string>>? VideoWallLayoutChanged;
    public event EventHandler<GenericTrippleEventArgs<string, string, string>>? VideoWallCellRouteChanged;
    public event EventHandler<GenericSingleEventArgs<string>>? VideoWallConnectionStatusChanged;

    public override void Initialize(IInfrastructureService hwService, IDomainService domain)
    {
        foreach (var wall in hwService.VideoWallDevices.GetAllDevices())
        {
            wall.VideoWallCellSourceChanged += WallOnVideoWallCellSourceChanged;
            wall.VideoWallLayoutChanged += WallOnVideoWallLayoutChanged;
            wall.ConnectionChanged += WallOnConnectionChanged;
        }
        
        base.Initialize(hwService, domain);
    }

    public override void SetActive()
    {
        foreach (var wall in HwService.VideoWallDevices.GetAllDevices() )
        {
            foreach (var canvas in wall.Canvases)
            {
                wall.SetActiveLayout(canvas.Id, canvas.StartupLayoutId);
            }
        }
        
        base.SetActive();
    }

    public ReadOnlyCollection<VideoWallInfoContainer> GetAllVideoWalls()
    {
        List<VideoWallInfoContainer> videoWalls = [];
        foreach (var wall in Domain.VideoWalls)
        {
            var device = HwService.VideoWallDevices.GetDevice(wall.Id);
            if (device == null) continue;

            List<VideoWallCanvasInfo> canvases = [];
            foreach (var canvas in device.Canvases)
            {
                List<VideoWallLayoutInfo> layouts = [];
                foreach (var layout in canvas.Layouts)
                {
                    List<VideoWallCellInfo> cells = [];
                    foreach (var cell in layout.Cells)
                    {
                        cells.Add(new VideoWallCellInfo(cell.Id, cell.Label, cell.Icon, cell.XPosition, cell.YPosition, cell.SourceId));
                    }
                    layouts.Add(new VideoWallLayoutInfo(layout.Id, layout.Width, layout.Height, layout.Id, layout.Label, layout.Icon, cells));    
                }
                
                canvases.Add(new VideoWallCanvasInfo(canvas.Id, canvas.Label, canvas.StartupLayoutId, canvas.MaxWidth, canvas.MaxHeight, layouts));
            }

            List<AvSourceInfoContainer> sources = [];
            foreach (var source in device.Sources)
            {
                sources.Add(new AvSourceInfoContainer(source.Id, source.Label, source.Icon, source.Tags, source.Control));
            }
            
            videoWalls.Add(new VideoWallInfoContainer(
                wall.Id,
                wall.Label,
                string.Empty,
                wall.Tags,device.IsOnline)
            {
                Manufacturer = string.IsNullOrEmpty(wall.Manufacturer) ? device.Manufacturer : wall.Manufacturer,
                Model = string.IsNullOrEmpty(wall.Model) ? device.Model : wall.Model,
                Canvases = new ReadOnlyCollection<VideoWallCanvasInfo>(canvases),
                Sources = new ReadOnlyCollection<AvSourceInfoContainer>(sources)
            });
        }

        return new ReadOnlyCollection<VideoWallInfoContainer>(videoWalls);
    }

    public List<AvSourceInfoContainer> QueryAllVideoWallSources(string controlId)
    {
        var controller = HwService.VideoWallDevices.GetDevice(controlId);
        if (controller == null) return [];
        List<AvSourceInfoContainer> sources = [];
        foreach (var source in controller.Sources)
        {
            sources.Add(new AvSourceInfoContainer(source.Id, source.Label, source.Icon, source.Tags, source.Control));
        }
        
        return sources;
    }

    public bool QueryVideoWallConnectionStatus(string controlId)
    {
        var device = HwService.VideoWallDevices.GetDevice(controlId);
        return device?.IsOnline == true;
    }

    public string QueryActiveVideoWallLayout(string controlId, string canvasId)
    {
        var device = HwService.VideoWallDevices.GetDevice(controlId);
        return device == null ? string.Empty : device.GetActiveLayoutId(canvasId);
    }

    public string QueryVideoWallCellSource(string controlId, string canvasId, string cellId)
    {
        var device = HwService.VideoWallDevices.GetDevice(controlId);
        return device == null ? string.Empty : device.GetCellSourceId(canvasId, cellId);
    }

    public void SetActiveVideoWallLayout(string controlId, string canvasId, string layoutId)
    {
        var device = HwService.VideoWallDevices.GetDevice(controlId);
        device?.SetActiveLayout(canvasId, layoutId);
    }

    public void SetVideoWallCellRoute(string controlId, string canvasId, string cellId, string sourceId)
    {
        var device = HwService.VideoWallDevices.GetDevice(controlId);
        device?.SetCellSource(canvasId, cellId, sourceId);
    }
    
    private void WallOnConnectionChanged(object? sender, GenericSingleEventArgs<string> e)
    {
        var temp = VideoWallConnectionStatusChanged;
        temp?.Invoke(this, e);
    }

    private void WallOnVideoWallLayoutChanged(object? sender, GenericSingleEventArgs<string> e)
    {
        
        if (sender is not IVideoWallDevice device) return;
        var temp = VideoWallLayoutChanged;
        temp?.Invoke(this, new GenericDualEventArgs<string, string>(device.Id, e.Arg));
    }

    private void WallOnVideoWallCellSourceChanged(object? sender, GenericDualEventArgs<string, string> e)
    {
        if (sender is not IVideoWallDevice device) return;
        var temp = VideoWallCellRouteChanged;
        temp?.Invoke(this, new GenericTrippleEventArgs<string, string, string>(device.Id, e.Arg1, e.Arg2));
    }
}