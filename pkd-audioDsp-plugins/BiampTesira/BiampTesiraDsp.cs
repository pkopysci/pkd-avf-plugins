using pkd_common_utils.GenericEventArgs;
using pkd_hardware_service.AudioDevices;
using pkd_hardware_service.BaseDevice;
using pkd_hardware_service.Routable;
using pkd_common_utils.Logging;

namespace BiampTesira;

public class BiampTesiraDsp : BaseDevice, IAudioRoutable, IDsp
{
    private bool _disposed;

    public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputLevelChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputMuteChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputLevelChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputMuteChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? AudioRouteChanged;
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    public void Initialize(
        string hostId,
        int coreId,
        string hostname,
        int port,
        string username,
        string password)
    {
        IsInitialized = false;
        Logger.Info("TODO: BiampTesiraDsp.Initialize()");
        //TODO: BiampTesiraDsp.Initialize()
        
        Id = hostId;
        IsInitialized = true;
    }

    public override void Connect()
    {
        if (!IsInitialized)
        {
            Logger.Error($"BiampTesiraDsp.Connect() - Must call Initialize() before calling Connect().");
            return;
        }

        Logger.Info("TODO: BiampTesiraDsp.Connect()");
        //TODO: BiampTesiraDsp.Connect()
    }

    public override void Disconnect()
    {
        if (!IsInitialized)
        {
            Logger.Error($"BiampTesiraDsp.Disconnect() - Must call Initialize() before calling Disconnect().");
            return;
        }

        Logger.Info("TODO: BiampTesiraDsp.Disconnect()");
        //TODO: BiampTesiraDsp.Disconnect()
    }

    public IEnumerable<string> GetAudioPresetIds()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> GetAudioInputIds()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> GetAudioOutputIds()
    {
        throw new NotImplementedException();
    }

    public void SetAudioInputLevel(string id, int level)
    {
        throw new NotImplementedException();
    }

    public int GetAudioInputLevel(string id)
    {
        throw new NotImplementedException();
    }

    public void SetAudioInputMute(string id, bool mute)
    {
        throw new NotImplementedException();
    }

    public bool GetAudioInputMute(string id)
    {
        throw new NotImplementedException();
    }

    public void SetAudioOutputLevel(string id, int level)
    {
        throw new NotImplementedException();
    }

    public int GetAudioOutputLevel(string id)
    {
        throw new NotImplementedException();
    }

    public void SetAudioOutputMute(string id, bool mute)
    {
        throw new NotImplementedException();
    }

    public bool GetAudioOutputMute(string id)
    {
        throw new NotImplementedException();
    }

    public void AddPreset(string id, int index)
    {
        throw new NotImplementedException();
    }
    
    public void RecallAudioPreset(string id)
    {
        throw new NotImplementedException();
    }

    public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin,
        int routerIndex)
    {
        throw new NotImplementedException();
    }

    public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int routerIndex, int bankIndex,
        int levelMax, int levelMin)
    {
        throw new NotImplementedException();
    }
    
    public string GetCurrentAudioSource(string outputId)
    {
        throw new NotImplementedException();
    }

    public void RouteAudio(string sourceId, string outputId)
    {
        throw new NotImplementedException();
    }

    public void ClearAudioRoute(string outputId)
    {
        throw new NotImplementedException();
    }
    
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // TODO: Release resources.

        }
        _disposed = true;
    }
}