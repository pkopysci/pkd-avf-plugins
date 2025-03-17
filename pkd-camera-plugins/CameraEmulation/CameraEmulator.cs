using System.Collections.ObjectModel;
using pkd_common_utils.DataObjects;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_hardware_service.BaseDevice;
using pkd_hardware_service.CameraDevices;
using pkd_hardware_service.PowerControl;

namespace CameraEmulation;

public class CameraEmulator : BaseDevice, ICameraDevice, IPowerControllable, IPanTiltDevice, IZoomDevice, IPresetDevice
{
    private List<CameraPreset> _presets = [];
    
    public event EventHandler<GenericSingleEventArgs<string>>? PowerChanged;

    public bool PowerState { get; private set; }

    public bool SupportsSavingPresets { get; } = true;
    
    
    public void Initialize(string hostname, int port, string id, string label, string username, string password)
    {
        IsInitialized = true;
        Id = id;
        Label = label;
    }

    public void SetPresetData(List<CameraPreset> presets)
    {
        _presets = presets;
    }

    public ReadOnlyCollection<CameraPreset> QueryAllPresets()
    {
        return new ReadOnlyCollection<CameraPreset>(_presets);
    }

    public override void Connect()
    {
        IsOnline = true;
        NotifyOnlineStatus();
    }

    public override void Disconnect()
    {
        IsOnline = false;
        NotifyOnlineStatus();
    }

    public void PowerOn()
    {
        PowerState = true;
        NotifyPowerChange();
    }

    public void PowerOff()
    {
        PowerState = false;
        NotifyPowerChange();
    }

    private void NotifyPowerChange()
    {
        Logger.Debug($"CameraEmulator {Id} - NotifyPowerChange() - newState: {PowerState}");
        var temp = PowerChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
    }

    public void SetPanTilt(Vector2D direction)
    {
       Logger.Debug($"CameraEmulator {Id} - SetPanTilt() - direction: ({direction.X}, {direction.Y})");
    }

    public void SetZoom(int speed)
    {
        Logger.Debug($"CameraEmulator {Id} - SetZoom() - speed: {speed}");
    }

    public void RecallPreset(string id)
    {
        Logger.Debug($"CameraEmulator {Id} - RecallPreset() - id: {id}");
    }

    public void SavePreset(string id)
    {
        Logger.Debug($"CameraEmulator {Id} - SavePreset() - id: {id}");
    }
}