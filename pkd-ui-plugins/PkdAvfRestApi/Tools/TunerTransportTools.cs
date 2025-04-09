using pkd_application_service.TransportControl;
using pkd_ui_service.Utility;

namespace PkdAvfRestApi.Tools;

public static class TunerTransportTools
{
    private static readonly Dictionary<string, TransportTypes> Transports = new()
    {
        { "POWERON", TransportTypes.PowerOn },
        { "POWEROFF", TransportTypes.PowerOff },
        { "POWERTOGGLE", TransportTypes.PowerToggle },
        { "DIAL", TransportTypes.Dial },
        { "DASH", TransportTypes.Dash },
        { "CHANUP", TransportTypes.ChannelUp },
        { "CHANDOWN", TransportTypes.ChannelDown },
        { "CHANSTOP", TransportTypes.ChannelStop },
        { "PAGEUP", TransportTypes.PageUp },
        { "PAGEDOWN", TransportTypes.PageDown },
        { "PAGESTOP", TransportTypes.PageStop },
        { "GUIDE", TransportTypes.Guide },
        { "MENU", TransportTypes.Menu },
        { "INFO", TransportTypes.Info },
        { "EXIT", TransportTypes.Exit },
        { "BACK", TransportTypes.Back },
        { "PLAY", TransportTypes.Play },
        { "PAUSE", TransportTypes.Pause },
        { "STOP", TransportTypes.Stop },
        { "RECORD", TransportTypes.Record },
        { "FWD", TransportTypes.ScanForward },
        { "REV", TransportTypes.ScanReverse },
        { "SKIPFWD", TransportTypes.SkipForward },
        { "SKIPREV", TransportTypes.SkipReverse },
        { "NAVUP", TransportTypes.NavUp },
        { "NAVDOWN", TransportTypes.NavDown },
        { "NAVLEFT", TransportTypes.NavLeft },
        { "NAVRIGHT", TransportTypes.NavRight },
        { "NAVSTOP", TransportTypes.NavStop },
        { "RED", TransportTypes.Red },
        { "YELLOW", TransportTypes.Yellow },
        { "GREEN", TransportTypes.Green },
        { "BLUE", TransportTypes.Blue },
        { "SELECT", TransportTypes.Select },
        { "PREV", TransportTypes.Previous },
        { "REPLAY", TransportTypes.Replay },
        { "DISHNET", TransportTypes.DishNet }
    };

    private static readonly Dictionary<TransportTypes, Action<ITransportControlApp, string>> Actions = new()
    {
        { TransportTypes.PowerOn, (app, devId) => { app.TransportPowerOn(devId); } },
        { TransportTypes.PowerOff, (app, devId) => { app.TransportPowerOff(devId); } },
        { TransportTypes.PowerToggle, (app, devId) => { app.TransportPowerToggle(devId); } },
        { TransportTypes.Dash, (app, devId) => { app.TransportDash(devId); } },
        { TransportTypes.ChannelUp, (app, devId) => { app.TransportChannelUp(devId); } },
        { TransportTypes.ChannelDown, (app, devId) => { app.TransportChannelDown(devId); } },
        { TransportTypes.PageUp, (app, devId) => { app.TransportPageUp(devId); } },
        { TransportTypes.PageDown, (app, devId) => { app.TransportPageDown(devId); } },
        { TransportTypes.Guide, (app, devId) => { app.TransportGuide(devId); } },
        { TransportTypes.Menu, (app, devId) => { app.TransportMenu(devId); } },
        { TransportTypes.Info, (app, devId) => { app.TransportInfo(devId); } },
        { TransportTypes.Exit, (app, devId) => { app.TransportExit(devId); } },
        { TransportTypes.Back, (app, devId) => { app.TransportBack(devId); } },
        { TransportTypes.Play, (app, devId) => { app.TransportPlay(devId); } },
        { TransportTypes.Pause, (app, devId) => { app.TransportPause(devId); } },
        { TransportTypes.Stop, (app, devId) => { app.TransportStop(devId); } },
        { TransportTypes.Record, (app, devId) => { app.TransportRecord(devId); } },
        { TransportTypes.ScanForward, (app, devId) => { app.TransportScanForward(devId); } },
        { TransportTypes.ScanReverse, (app, devId) => { app.TransportScanReverse(devId); } },
        { TransportTypes.SkipForward, (app, devId) => { app.TransportSkipForward(devId); } },
        { TransportTypes.SkipReverse, (app, devId) => { app.TransportSkipReverse(devId); } },
        { TransportTypes.NavUp, (app, devId) => { app.TransportNavUp(devId); } },
        { TransportTypes.NavDown, (app, devId) => { app.TransportNavDown(devId); } },
        { TransportTypes.NavLeft, (app, devId) => { app.TransportNavLeft(devId); } },
        { TransportTypes.NavRight, (app, devId) => { app.TransportNavRight(devId); } },
        { TransportTypes.Red, (app, devId) => { app.TransportRed(devId); } },
        { TransportTypes.Green, (app, devId) => { app.TransportGreen(devId); } },
        { TransportTypes.Yellow, (app, devId) => { app.TransportYellow(devId); } },
        { TransportTypes.Blue, (app, devId) => { app.TransportBlue(devId); } },
        { TransportTypes.Select, (app, devId) => { app.TransportSelect(devId); } }
    };

    public static TransportTypes FindTransport(string data)
    {
        return Transports.GetValueOrDefault(data, TransportTypes.Unknown);
    }

    public static void SendCommand(ITransportControlApp appService, string id, TransportTypes transportType)
    {
        if (Actions.TryGetValue(transportType, out var act)) act.Invoke(appService, id);
    }
}