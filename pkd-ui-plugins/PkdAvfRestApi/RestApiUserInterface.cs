using Crestron.SimplSharpPro;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using pkd_application_service;
using pkd_application_service.UserInterface;
using pkd_common_utils.FileOps;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using PkdAvfRestApi.Configuration;
using PkdAvfRestApi.Endpoints;
using PkdAvfRestApi.Extensions;
using PkdAvfRestApi.Services;
using PkdAvfRestApi.Tools;

namespace PkdAvfRestApi;

public class RestApiUserInterface : IUserInterface, ICrestronUserInterface, IUsesApplicationService, IDisposable
{
    private static readonly CancellationTokenSource CancellationTokenSource = new();
    private WebApplication? _app;
    private CrestronControlSystem? _controlSystem;
    private IApplicationService? _applicationService;
    private bool _disposed;
    private string _configPath = string.Empty;
    
    public event EventHandler<GenericSingleEventArgs<string>>? OnlineStatusChanged;

    public bool IsInitialized { get; private set; }
    public bool IsOnline { get; private set; }
    public bool IsXpanel { get; } = false;
    public string Id { get; private set; } = string.Empty;

    ~RestApiUserInterface()
    {
        Dispose(false);
    }

    public void Initialize()
    {
        Logger.Debug("AVF Rest API - Initialize()");

        if (_controlSystem == null)
        {
            Logger.Error("AVF Rest API - Initialize() - call SetCrestronControl() first.");
            return;
        }

        if (_applicationService == null)
        {
            Logger.Error("AVF Rest API - Initialize() - call SetApplicationService() first.");
            return;
        }

        if (string.IsNullOrEmpty(_configPath))
        {
            Logger.Error(
                "AVF Rest API - Initialize() - either call SetUiData() or confirm that the SgdFile property is not null or empty.");
            return;
        }

        var builder = WebApplication.CreateBuilder();
        var configPath = DirectoryHelper.NormalizePath($"{DirectoryHelper.GetUserFolder()}/net8-plugins/{_configPath}");
        builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: false);

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
        services.AddCors();

        _app = builder.Build();
        _app.UseCors(b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        _app.UseMiddleware<BasicAuthMiddleware>();
        
        _app.MapSystemEndpoints(_applicationService);
        _app.MapDisplayEndpoints(_applicationService);
        _app.MapGlobalVideoEndpoints(_applicationService);
        _app.MapVideoRoutingEndpoints(_applicationService);
        _app.MapVideoWallEndpoints(_applicationService);
        _app.MapAudioEndpoints(_applicationService);
        _app.MapLightingEndpoints(_applicationService);
        _app.MapTunerEndpoints(_applicationService);
        _app.MapCameraEndpoints(_applicationService);
        

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
        _configPath = uiData.SgdFile;
    }

    public void SetCrestronControl(CrestronControlSystem parent)
    {
        Logger.Debug("AVF Rest API - SetCrestronControl()");
        _controlSystem = parent;
    }

    public void SetApplicationService(IApplicationService applicationService)
    {
        Logger.Debug("AVF Rest API - SetApplicationService()");

        ArgumentNullException.ThrowIfNull(applicationService);
        _applicationService = applicationService;
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