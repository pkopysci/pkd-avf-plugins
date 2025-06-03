using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Configuration;
using PkdAvfRestApi.Tools;

namespace PkdAvfRestApi.Services;

public sealed class ProgramService : BackgroundService
{
    private readonly ILogger<ProgramService> _logger;
    private readonly IControlSystemContext _controlSystemContext;

    private readonly HostConfig _hostConfig;

    public ProgramService(ILogger<ProgramService> logger, IOptions<HostConfig> hostConfig, IControlSystemContext controlSystemContext)
    {
        _logger = logger;
        _controlSystemContext = controlSystemContext;
        _hostConfig = hostConfig.Value;

        TaskScheduler.UnobservedTaskException += (_, args) => logger.LogError(args.Exception, "An unobserved task exception occurred");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Logger.Info($"REST API started.");
            //_logger.LogInformation("Program service started");
            // _logger.LogInformation("Control System: {ControlSystem}", _controlSystemContext.ControlSystem.ControllerPrompt);
            // _logger.LogInformation("Configuration: {ConfigurationPath}", _hostConfig.ConfigurationPath);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred in the REST API.");
        }

        return Task.CompletedTask;
    }
}