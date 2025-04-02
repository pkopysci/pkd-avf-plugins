using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using pkd_common_utils.Logging;
using PkdAvfRestApi;


namespace UiTesting;

public class ControlSystem : CrestronControlSystem
{
    private static readonly CancellationTokenSource CancellationTokenSource = new();
    private readonly CrestronTextWriter _writer = new();
    private RestApiUserInterface? _server;
    
    public ControlSystem()
    {
        try
        {
            Crestron.SimplSharpPro.CrestronThread.Thread.MaxNumberOfUserThreads = 100;
            CrestronEnvironment.ProgramStatusEventHandler += ControllerProgramEventHandler;
            Console.SetOut(_writer);
            CrestronConsole.AddNewConsoleCommand(RunTest, "test", "", ConsoleAccessLevelEnum.AccessOperator);
        }
        catch (Exception exception)
        {
            ErrorLog.Error("ControlSystem.Ctor()", exception.Message);
        }
    }

    public override void InitializeSystem()
    {
        try
        {
            Logger.SetProgramId($"PGM {ProgramNumber}");
            Logger.SetDebugOn();
        }
        catch (Exception exception)
        {
            ErrorLog.Error("ControlSystem.InitializeSystem()", exception.Message);
        }
    }

    private void RunTest(string cmd)
    {
        _server = new RestApiUserInterface();
        _server.SetCrestronControl(this, 8080);
        _server.Initialize();
        _server.Connect();
    }
    
    private void ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
    {
        if (programStatusEventType != eProgramStatusEventType.Stopping) return;
        CancellationTokenSource.Cancel();
        _writer.Dispose();
        _server?.Dispose();
    }
}