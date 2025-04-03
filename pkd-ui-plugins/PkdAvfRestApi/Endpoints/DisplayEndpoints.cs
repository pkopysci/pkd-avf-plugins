using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;

namespace PkdAvfRestApi.Endpoints;

public static class DisplayEndpoints
{
    private const string GetDisplayEndpointName = "GetDisplay";
    private static IApplicationService? _appService;

    public static RouteGroupBuilder MapDisplayEndpoints(this WebApplication app, IApplicationService service)
    {
        _appService = service;

        var group = app.MapGroup("displays");

        group.MapGet("/", () => _appService.GetAllDisplayInfo())
            .WithName("Displays")
            .WithOpenApi();

        group.MapGet("/{id}", (string id) =>
            {
                var found = _appService.GetAllDisplayInfo().FirstOrDefault(x => x.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(found);
            })
            .WithName(GetDisplayEndpointName);

        return group;
    }
}