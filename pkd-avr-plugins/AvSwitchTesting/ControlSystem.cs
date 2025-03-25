using CarboniteAvrs;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;

namespace AvSwitchTesting;

public class ControlSystem : CrestronControlSystem
{
    private BasicCarboniteUltra _avr = new();

    public ControlSystem()
    {
        CrestronConsole.AddNewConsoleCommand(Connect, "connect", "", ConsoleAccessLevelEnum.AccessOperator);
        CrestronConsole.AddNewConsoleCommand(Disconnect, "disconnect", "", ConsoleAccessLevelEnum.AccessOperator);
        CrestronConsole.AddNewConsoleCommand(Test, "test", "[input],[output]", ConsoleAccessLevelEnum.AccessOperator);
        CrestronConsole.AddNewConsoleCommand(ClearRoute, "clear", "", ConsoleAccessLevelEnum.AccessOperator);
    }

    public override void InitializeSystem()
    {
        try
        {
            Logger.SetProgramId("AVR TEST");
            Logger.SetDebugOn();
            _avr.Initialize(
                "10.16.6.168",
                7788,
                "avr01",
                "Carbonite Ultra");
            _avr.ConnectionChanged += AvrOnConnectionChanged;
            _avr.VideoRouteChanged += AvrOnVideoRouteChanged;
        }
        catch (Exception exception)
        {
            CrestronConsole.PrintLine(
                $"Error in InitializeSystem() - {exception.Message}: {exception.StackTrace?.Replace("\n", "\n\r")}");
        }
    }

    private void AvrOnVideoRouteChanged(object? sender, GenericDualEventArgs<string, uint> genericDualEventArgs)
    {
       Logger.Info("TODO: ControlSystem.AvrOnVideoRouteChanged()");
    }

    private void AvrOnConnectionChanged(object? sender, GenericSingleEventArgs<string> genericSingleEventArgs)
    {
        Logger.Info("TODO: ControlSystem.AvrOnConnectionChanged()");
    }

    private void Connect(string arg)
    {
        if (_avr is not { IsOnline: true })
        {
            Logger.Debug("ControlSystem.Connect()");
            _avr.Connect();
        }
    }

    private void Disconnect(string arg)
    {
        if (_avr is { IsOnline: true })
        {
            Logger.Debug("ControlSystem.Disconnect()");
            _avr.Disconnect();
        }
    }

    private void Test(string arg)
    {
        Logger.Debug($"ControlSystem.Test({arg})");
        var args = arg.Split(',');
        var source = uint.Parse(args[0]);
        var target = uint.Parse(args[1]);
        _avr.RouteVideo(source, target);
    }

    private void ClearRoute(string arg)
    {
        Logger.Debug($"ControlSystem.ClearRoute({arg})");
        _avr.ClearVideoRoute(uint.Parse(arg));
    }
}