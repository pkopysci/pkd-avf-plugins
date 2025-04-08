using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Video.Global;

namespace PkdAvfRestApi.Endpoints;

internal static class GlobalVideoEndpoints
{
    private static IApplicationService? _appService;

    public static RouteGroupBuilder MapGlobalVideoEndpoints(this WebApplication app, IApplicationService appService)
    {
        _appService = appService;
        var group = app.MapGroup("video/global");

        group.MapGet("/supported", () => Results.Ok(new SupportedDto(_appService != null)));
        
        group.MapGet("/", () =>
        {
            if (_appService == null) return Results.BadRequest();

            try
            {
                return Results.Ok(new GlobalVideoDto(
                    _appService.QueryGlobalVideoBlank(),
                    _appService.QueryGlobalVideoFreeze()
                ));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET global video state.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/", (SetGlobalVideoDto dto) =>
        {
            if (_appService == null) return Results.BadRequest();
            try
            {
                if (_appService.QueryGlobalVideoBlank() != dto.Blank)
                {
                    _appService.SetGlobalVideoBlank(dto.Blank);
                }

                if (_appService.QueryGlobalVideoFreeze() != dto.Freeze)
                {
                    _appService.SetGlobalVideoFreeze(dto.Freeze);
                }
                
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT global video state.");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }
}