using BiampTesira;
using BiampTesira.Coms;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using pkd_common_utils.Logging;

namespace DspPluginTesting;

public class ControlSystem : CrestronControlSystem
{
    private readonly TesiraComsManager _comsManager;

    public ControlSystem()
    {
        try
        {
            CrestronEnvironment.ProgramStatusEventHandler += ControllerProgramEventHandler;
            CrestronConsole.AddNewConsoleCommand(Connect, "connect", "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(Disconnect, "disconnect", "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(SendTestCommands, "test", "", ConsoleAccessLevelEnum.AccessOperator);

            _comsManager = new TesiraComsManager("10.21.47.11", 23, "admin", "password");
        }
        catch (Exception exception)
        {
            CrestronConsole.PrintLine(exception.ToString());
            throw;
        }
    }

    public override void InitializeSystem()
    {
        try
        {
            Logger.SetProgramId("DSP TEST");
            Logger.SetDebugOn();
            _comsManager.ConnectionChanged += ComsManagerOnConnectionChanged;
            _comsManager.ReadyForCommandsReceived += ComsManagerOnReadyForCommandsReceived;
        }
        catch (Exception exception)
        {
            CrestronConsole.PrintLine(exception.ToString());
            throw;
        }
    }

    private void ComsManagerOnReadyForCommandsReceived(object? sender, EventArgs eventArgs)
    {
        Logger.Info(
            $"ControlSystem.ComsManagerOnReadyForCommandsReceived() -  new state: {_comsManager.ReadyForCommands}");
    }

    private void ComsManagerOnConnectionChanged(object? sender, EventArgs eventArgs)
    {
        Logger.Info($"ControlSystem - ComsManager connection change handler. new state: {_comsManager.Connected}");
    }

    private void Connect(string arg)
    {
        _comsManager.Connect();
    }

    private void Disconnect(string arg)
    {
        _comsManager.Disconnect();
    }

    private void SendTestCommands(string arg)
    {
        Logger.Info("ControlSystem.SendTestCommands()");
        for (var i = 5; i <= 7; i++)
        {
            TesiraComsData data = new()
            {
                InstanceTag = "matrixmxr",
                BlockType = BlockTypes.Router,
                ChannelId = "c01",
                SerializedCommand = $"matrixmxr get crosspoint 16 {i}\n"
            };

            _comsManager.SendCommand(data);
        }
    }

    private void ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
    {
        switch (programStatusEventType)
        {
            case (eProgramStatusEventType.Paused):
                //The program has been paused.  Pause all user threads/timers as needed.
                break;
            case (eProgramStatusEventType.Resumed):
                //The program has been resumed. Resume all the user threads/timers as needed.
                break;
            case (eProgramStatusEventType.Stopping):
                _comsManager.Dispose();
                break;
        }
    }
}