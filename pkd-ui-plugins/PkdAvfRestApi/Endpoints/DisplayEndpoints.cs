using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using PkdAvfRestApi.Contracts;

namespace PkdAvfRestApi.Endpoints;

public static class DisplayEndpoints
{
    private const string GetDisplayEndpointName = "GetDisplay";

    private static readonly List<DisplayDto> Displays =
    [
        new(1, "Projector", "NEC", "ABC-123", true, true, true, []),
        new(2, "Display 1", "NEC", "FP-55", true, false, true, []),
        new(3, "Display 2", "NEC", "FP-55", false, false, true, ["station"])
    ];

    private static ApplicationService? _appService;

    public static RouteGroupBuilder MapDisplayEndpoints(this WebApplication app, ApplicationService service)
    {
        _appService = service;
        
        var group = app.MapGroup("displays");
        
        group.MapGet("/", () => Displays)
            .WithName("Displays")
            .WithOpenApi();

        return group;
    }
}