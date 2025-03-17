using System.Collections.ObjectModel;
using pkd_application_service;
using pkd_application_service.Base;
using pkd_application_service.CameraControl;
using pkd_common_utils.DataObjects;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_domain_service;
using pkd_domain_service.Data.CameraData;
using pkd_hardware_service;
using pkd_hardware_service.CameraDevices;
using pkd_hardware_service.PowerControl;

// ReSharper disable SuspiciousTypeConversion.Global

namespace CameraTestingAppService;

public class CameraTestingApp : ApplicationService, ICameraControlApp
{
    public event EventHandler<GenericSingleEventArgs<string>>? CameraControlConnectionChanged;
    public event EventHandler<GenericSingleEventArgs<string>>? CameraPowerStateChanged;

    public override void Initialize(IInfrastructureService hwService, IDomainService domain)
    {
        foreach (var device in hwService.CameraDevices.GetAllDevices())
        {
            device.ConnectionChanged += CameraConnectionHandler;
            if (device is IPowerControllable powerCam)
            {
                powerCam.PowerChanged += CameraPowerChangeHandler;
            }
        }
        
        base.Initialize(hwService, domain);
    }

    public ReadOnlyCollection<CameraInfoContainer> GetAllCameraDeviceInfo()
    {
        var devices = new List<CameraInfoContainer>();
        foreach (var config in Domain.Cameras)
        {
            var device = HwService.CameraDevices.GetDevice(config.Id);
            if (device == null) continue;
            try
            {
                devices.Add(CreateCameraInfoContainer(device, config));
            }
            catch (Exception e)
            {
                Logger.Error($"CameraTestingApp.GetAllCameraDeviceInfo() - failed to add data for {config.Id} - {e.Message}");
            }
        }
        
        return new ReadOnlyCollection<CameraInfoContainer>(devices);
    }

    public void SendCameraPanTilt(string cameraId, Vector2D direction)
    {
        var camera = HwService.CameraDevices.GetDevice(cameraId);
        if (camera is not IPanTiltDevice ptzCam) return;
        ptzCam.SetPanTilt(direction);        
    }

    public void SendCameraZoom(string cameraId, int speed)
    {
        var camera = HwService.CameraDevices.GetDevice(cameraId);
        if (camera is not IZoomDevice zoomCam) return;
        zoomCam.SetZoom(speed);
    }

    public void SendCameraPresetRecall(string cameraId, string presetId)
    {
        Logger.Debug($"CameraTestingApp.SendCameraPresetRecall({cameraId}, {presetId})");
        
        var camera = HwService.CameraDevices.GetDevice(cameraId);
        if (camera is not IPresetDevice presetCam) return;
        
        Logger.Debug($"CameraTestingApp.SendCameraPresetRecall() - found camera with ID {cameraId}");
        presetCam.RecallPreset(presetId);
    }

    public void SendCameraPresetSave(string cameraId, string presetId)
    {
        var camera = HwService.CameraDevices.GetDevice(cameraId);
        if (camera is not IPresetDevice { SupportsSavingPresets: true } presetCam) return;
        presetCam.SavePreset(presetId);
    }

    public bool QueryCameraConnectionStatus(string id)
    {
        var camera = HwService.CameraDevices.GetDevice(id);
        return camera is { IsOnline: true };
    }

    public bool QueryCameraPowerStatus(string id)
    {
        var camera = HwService.CameraDevices.GetDevice(id);
        return camera is IPowerControllable { PowerState: true };
    }

    public void SendCameraPowerChange(string id, bool newState)
    {
        var camera = HwService.CameraDevices.GetDevice(id);
        if (camera is not IPowerControllable powerCam) return;
        if (newState)
        {
            powerCam.PowerOn();
        }
        else
        {
            powerCam.PowerOff();
        }
    }

    private static CameraInfoContainer CreateCameraInfoContainer(ICameraDevice device, Camera config)
    {
        var presets = new List<InfoContainer>();
        foreach (var presetConfig in config.Presets)
        {
            presets.Add(new InfoContainer(presetConfig.Id, presetConfig.Label, string.Empty, [], true));
        }

        return new CameraInfoContainer(config.Id, config.Label, string.Empty, config.Tags, device.IsOnline)
        {
            Model = device.Model,
            Manufacturer = device.Manufacturer,
            SupportsZoom = device is IZoomDevice,
            SupportsPanTilt = device is IPanTiltDevice,
            SupportsSavingPresets = (device is IPresetDevice { SupportsSavingPresets: true }),
            Presets = presets,
        };
    }

    private void CameraConnectionHandler(object? sender, GenericSingleEventArgs<string> args)
    {
        var temp = CameraControlConnectionChanged;
        temp?.Invoke(this, args);
    }

    private void CameraPowerChangeHandler(object? sender, GenericSingleEventArgs<string> args)
    {
        var temp = CameraPowerStateChanged;
        temp?.Invoke(this, args);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var camera in HwService.CameraDevices.GetAllDevices())
            {
                camera.ConnectionChanged -= CameraConnectionHandler;
                if (camera is IPowerControllable powerCam)
                {
                    powerCam.PowerChanged -= CameraPowerChangeHandler;
                }
            }
        }
        
        base.Dispose(disposing);
    }
}