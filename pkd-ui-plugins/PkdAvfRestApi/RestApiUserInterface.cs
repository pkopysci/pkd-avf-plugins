using Crestron.SimplSharpPro;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using pkd_application_service;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using PkdAvfRestApi.Configuration;
using PkdAvfRestApi.Endpoints;
using PkdAvfRestApi.Extensions;
using PkdAvfRestApi.Services;
using PkdAvfRestApi.Tools;

namespace PkdAvfRestApi;

public class RestApiUserInterface : IUserInterface, ICrestronUserInterface, IDisposable
{
    private static readonly CancellationTokenSource CancellationTokenSource = new();
    private WebApplication? _app;
    private CrestronControlSystem? _controlSystem;
    private ApplicationService? _applicationService;
    private bool _disposed;

    public event EventHandler<GenericSingleEventArgs<bool>>? SystemStateChangeRequest;
    public event EventHandler<GenericSingleEventArgs<string>>? OnlineStatusChanged;
    public event EventHandler? GlobalFreezeToggleRequest;
    public event EventHandler? GlobalBlankToggleRequest;

    public bool IsInitialized { get; private set; }
    public bool IsOnline { get; private set; }
    public bool IsXpanel { get; } = false;
    public string Id { get; private set; } = string.Empty;

    ~RestApiUserInterface()
    {
        Dispose(false);
    }

    public void SetSystemState(bool state)
    {
        throw new NotImplementedException();
    }

    public void ShowSystemStateChanging(bool state)
    {
        throw new NotImplementedException();
    }

    public void HideSystemStateChanging()
    {
        throw new NotImplementedException();
    }

    public void SetGlobalFreezeState(bool state)
    {
        throw new NotImplementedException();
    }

    public void SetGlobalBlankState(bool state)
    {
        throw new NotImplementedException();
    }

    public void Initialize()
    {
        Logger.Debug("AVF Rest API - Initialize()");
        
        if (_controlSystem == null)
        {
            Logger.Error("AVF Rest API - Initialize() - call SetCrestronControl() first.");
            return;
        }

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddJsonFile("ProgramConfig.json", optional: false, reloadOnChange: false);

        var services = builder.Services;
        builder.Configure<HostConfig>("HostConfig");

        services.AddSingleton<IControlSystemContext, ControlSystemContext>(
            _ => new ControlSystemContext(_controlSystem));

        services.Configure<HostOptions>(options =>
        {
            options.ServicesStartConcurrently = true;
            options.ServicesStopConcurrently = true;
        });

        services.AddHostedService<ProgramService>();
        services.AddEndpointsApiExplorer();

        _app = builder.Build();
        _app.MapDisplayEndpoints(new ApplicationService());
        
        _app.Lifetime.ApplicationStarted.Register(() =>
        {
            foreach (var url in _app.Urls)
            {
                var port = new Uri(url).Port;
                PortForwardFactory.TryCreateTcp(new PortForward(port));
            }
        });

        IsInitialized = true;
    }

    public void Connect()
    {
        if (!IsInitialized || _app == null)
        {
            Logger.Error("AVF Rest API - call Initialize() first.");
            return;
        }

        _ = Task.Run(async () => await _app.RunAsync(CancellationTokenSource.Token).ConfigureAwait(false));

        IsOnline = true;
        var temp = OnlineStatusChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
    }

    public void SetUiData(UserInterfaceDataContainer uiData)
    {
        Logger.Debug("AVF Rest API - SetUiData()");
        Id = uiData.Id;
    }

    public void SetCrestronControl(CrestronControlSystem parent, int _)
    {
        Logger.Debug("AVF Rest API - SetCrestronControl()");
        _controlSystem = parent;
    }

    public void SetApplicationService(ApplicationService service)
    {
        Logger.Debug("AVF Rest API - SetApplicationService()");
        _applicationService = service;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            CancellationTokenSource.Cancel();
        }

        _disposed = true;
    }
}