namespace VideoWallAppService;

using System.Collections.ObjectModel;
using pkd_application_service;
using pkd_application_service.VideoWallControl;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_domain_service;
using pkd_domain_service.Data.VideoWallData;
using pkd_hardware_service;
using pkd_hardware_service.VideoWallDevices;


public class VideoWallApplication : ApplicationService, IVideoWallApp
{
    /// <inheritdoc />
    public event EventHandler<GenericSingleEventArgs<string>>? VideoWallLayoutChanged;

    /// <inheritdoc />
    public event EventHandler<GenericDualEventArgs<string, string>>? VideoWallCellRouteChanged;

    /// <inheritdoc />
    public event EventHandler<GenericSingleEventArgs<string>>? VideoWallConnectionStatusChanged;
    
    /// <summary>
    /// Initialize all event subscriptions with the hardware and domain service. This also calls the Initialize() method
    /// on the base ApplicationService.
    /// </summary>
    /// <param name="hwService">The device control service that manages hardware interactions.</param>
    /// <param name="domain">The configuration domain that contains all of the system configuration.</param>
    public override void Initialize(IInfrastructureService hwService, IDomainService domain)
    {
        foreach (var device in hwService.VideoWallDevices.GetAllDevices())
        {
            device.ConnectionChanged += HandleDeviceOnlineChange;
            device.VideoWallLayoutChanged += HandleVideoWallLayoutChanged;
            device.VideoWallCellSourceChanged += HandleVideoWallCellRouteChanged;
        }
        
        base.Initialize(hwService, domain);
    }

    /// <summary>
    /// Get all video wall controllers that exist in the system configuration. Writes an error to the logging system if
    /// there is any problems getting the video wall data.
    /// </summary>
    /// <returns>All video wall controller information in the system. Returns an empty collection if a problem occurs.</returns>
    public ReadOnlyCollection<VideoWallInfoContainer> GetAllVideoWalls()
    {
        var infoContainers = new List<VideoWallInfoContainer>();
        foreach (var wallConfig in Domain.VideoWalls)
        {
            var device = HwService.VideoWallDevices.GetDevice(wallConfig.Id);
            if (device == null) continue;
            try
            {
                infoContainers.Add(CreateVideoWallContainer(device, wallConfig));
            }
            catch (Exception e)
            {
                Logger.Error($"VideoWallApplication.GetAllVideoWalls() - Failed to add video wall data for {wallConfig.Id}: {e.Message}");
            }
        }
        
        return new ReadOnlyCollection<VideoWallInfoContainer>(infoContainers);
    }

    /// <summary>
    /// query the target device to see if it is online and communicating with the framework.
    /// Logs an error if the target device cannot be found.
    /// </summary>
    /// <param name="controlId">The id of the video wall controller to query</param>
    /// <returns>true if the device is online, false if it is offline or a device with the given id was not found.</returns>
    public bool QueryVideoWallConnectionStatus(string controlId)
    {
        var device = FindVideoWallDevice(controlId, nameof(QueryVideoWallConnectionStatus));
        return device?.IsOnline ?? false;
    }

    /// <summary>
    /// Query the target video wall control for the layout that is currently in use.
    /// Logs an error if the target device cannot be found.
    /// </summary>
    /// <param name="controlId">the id of the video wall control to query.</param>
    /// <returns>the id of the active layout. Returns an empty string if the target controller cannot be found.</returns>
    public string QueryActiveVideoWallLayout(string controlId)
    {
        var device = FindVideoWallDevice(controlId, nameof(QueryActiveVideoWallLayout));
        return device?.GetActiveLayoutId() ?? string.Empty;
    }

    /// <summary>
    /// Query the target video wall controller for the source that is routed to the target cell. The cell being queried
    /// is expected to be a valid cell in the currently active layout.
    /// An error is logged if the target control cannot be found.
    /// </summary>
    /// <param name="controlId">the id of the video wall control to query.</param>
    /// <param name="cellId">The id of the cell in the currently active layout to query.</param>
    /// <returns>the id of the source currently routed to the cell. returns an empty string if a problem occurs.</returns>
    public string QueryVideoWallCellSource(string controlId, string cellId)
    {
        var device = FindVideoWallDevice(controlId, nameof(QueryVideoWallCellSource));
        return device == null ? string.Empty : device.GetCellSourceId(cellId);
    }

    /// <summary>
    /// Send a request to the hardware service to change the video wall layout on the target control device.
    /// Logs an error if any problems are encountered.
    /// </summary>
    /// <param name="controlId">the id of the video wall controller to control.</param>
    /// <param name="layoutId">the id of the layout to set as active.</param>
    public void SetActiveVideoWallLayout(string controlId, string layoutId)
    {
        var device = FindVideoWallDevice(controlId, nameof(SetActiveVideoWallLayout));
        try
        {
            device?.SetActiveLayout(layoutId);
        }
        catch (Exception e)
        {
            Logger.Error($"VideoWallApplication.{nameof(SetActiveVideoWallLayout)}() - Failed to set video wall {controlId} layout {layoutId}: {e.Message}");
        }
    }

    /// <summary>
    /// Send a video route request to the hardware service for the target window/cell. Logs an error if any problems
    /// are encountered.
    /// </summary>
    /// <param name="controlId">The id of the video wall control to change.</param>
    /// <param name="cellId">the id of the cell in the active layout to change.</param>
    /// <param name="sourceId">the id of the video source to route to the target cell.</param>
    public void SetVideoWallCellRoute(string controlId, string cellId, string sourceId)
    {
        var device = FindVideoWallDevice(controlId, nameof(SetVideoWallCellRoute));
        try
        {
            device?.SetCellSource(cellId, sourceId);
        }
        catch (Exception e)
        {
            Logger.Error($"VideoWallApplication.{nameof(SetVideoWallCellRoute)}() - Failed to set video wall {controlId} route for  {cellId}: {e.Message}");
        }
    }

    private void HandleDeviceOnlineChange(object? device, GenericSingleEventArgs<string> args)
    {
        var temp = VideoWallConnectionStatusChanged;
        temp?.Invoke(this, args);
    }

    private void HandleVideoWallLayoutChanged(object? device, EventArgs args)
    {
        if (device is not IVideoWallDevice videoWallDevice) return;
        var temp = VideoWallLayoutChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(videoWallDevice.Id));
    }

    private void HandleVideoWallCellRouteChanged(object? device, GenericSingleEventArgs<string> args)
    {
        if (device is not IVideoWallDevice videoWallDevice) return;
        var temp = VideoWallCellRouteChanged;
        temp?.Invoke(this, new GenericDualEventArgs<string, string>(videoWallDevice.Id, args.Arg) );
    }
    private IVideoWallDevice? FindVideoWallDevice(string controlId, string methodName)
    {
        var device = HwService.VideoWallDevices.GetDevice(controlId);
        if (device == null)
        {
            Logger.Error($"VideoWallApplication.{methodName}() - No video wall device with id {controlId}");
        }

        return device;
    }
    
    private static VideoWallInfoContainer CreateVideoWallContainer(IVideoWallDevice device, VideoWall config)
    {
        var layouts = new List<VideoWallLayoutInfo>();
        foreach (var layout in device.Layouts)
        {
            var cells = new List<VideoWallCellInfo>();
            foreach (var cell in layout.Cells)
            {
                cells.Add(new VideoWallCellInfo
                {
                    Id = cell.Id,
                    SourceId = cell.SourceId,
                    XStart = cell.XStart,
                    XEnd = cell.XEnd,
                    YStart = cell.YStart,
                    YEnd = cell.YEnd,
                });
            }

            layouts.Add(new VideoWallLayoutInfo
            {
                Id = layout.Id,
                Label = layout.Label,
                VideoWallControlId = config.Id,
                Cells = cells
            });
        }
            
        var wallInfo = new VideoWallInfoContainer(
            config.Id,
            config.Label,
            string.Empty,
            config.Tags,
            device.IsOnline)
        {
            Layouts = new ReadOnlyCollection<VideoWallLayoutInfo>(layouts)
        };
        
        return wallInfo;
    }
}