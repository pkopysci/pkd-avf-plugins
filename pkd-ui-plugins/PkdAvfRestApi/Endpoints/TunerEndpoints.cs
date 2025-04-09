using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_application_service.Base;
using pkd_application_service.TransportControl;
using pkd_common_utils.Logging;
using pkd_ui_service.Utility;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Tuners;
using PkdAvfRestApi.Tools;

// ReSharper disable RedundantCast
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace PkdAvfRestApi.Endpoints;

internal static class TunerEndpoints
{
    private static ITransportControlApp? _appService;

    public static RouteGroupBuilder MapTunerEndpoints(this WebApplication app, IApplicationService appService)
    {
        _appService = appService as ITransportControlApp;
        if (_appService == null)
            Logger.Warn(
                "AVF REST Api - Video Wall Endpoints - provided IApplicationService does not implement IVideoWallApp.");

        var group = app.MapGroup("endpoints/tuners");

        group.MapGet("/supported", () => Results.Ok(new SupportedDto(_appService != null)));

        group.MapGet("/", () =>
        {
            if (_appService == null) return Results.BadRequest("Transport Tuners are not supported.");
            try
            {
                List<TunerDto> tuners = [];
                foreach (var tuner in _appService.GetAllCableBoxes()) tuners.Add(CreateTunerDto(tuner));

                return Results.Ok(tuners);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - GET Tuner Devices.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Transport Tuners are not supported.");
            try
            {
                var found = _appService.GetAllCableBoxes()
                    .FirstOrDefault(t => t.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateTunerDto(found));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET Tuner Device {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/favorites/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Transport Tuners are not supported.");
            try
            {
                var found = _appService.GetAllCableBoxes().FirstOrDefault(x => x.Id == id);
                return found == null ? Results.NotFound(id) : Results.Ok(CreateTunerFavoritesList(found.Favorites));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET tuner device {id} transports.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/favorites/{id}", (string id, SetTunerFavoriteDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Transport Tuners are not supported.");
            try
            {
                if (string.IsNullOrEmpty(body.Id)) return Results.BadRequest("Missing favorite Id.");
                var found = _appService.GetAllCableBoxes().FirstOrDefault(t => t.Id == id);
                if (found == null) return Results.NotFound(id);

                _appService.TransportDialFavorite(id, body.Id);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT tuner device {id} favorites.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/dial/{id}", (string id, SetTunerChannelDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Transport Tuners are not supported.");
            try
            {
                if (string.IsNullOrEmpty(body.Channel)) return Results.BadRequest("Missing Channel.");
                var found = _appService.GetAllCableBoxes().FirstOrDefault(t => t.Id == id);
                if (found == null) return Results.NotFound(id);

                _appService.TransportDial(id, body.Channel);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT tuner device {id} dial channel.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/transports/{id}", (string id, SetTunerTransportDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Transport Tuners are not supported.");
            try
            {
                if (string.IsNullOrEmpty(body.Transport)) return Results.BadRequest("Missing Transport.");
                var found = _appService.GetAllCableBoxes().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound(id);

                var command = TunerTransportTools.FindTransport(body.Transport);
                if (command == TransportTypes.Unknown) return Results.BadRequest($"Unknown transport {body.Transport}");

                TunerTransportTools.SendCommand(_appService, id, command);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT tuner device {id} transport command.");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }

    private static List<TunerFavoriteDto> CreateTunerFavoritesList(List<TransportFavorite> favoritesCollection)
    {
        List<TunerFavoriteDto> favorites = [];
        foreach (var favorite in favoritesCollection)
            favorites.Add(new TunerFavoriteDto(
                favorite.Id,
                favorite.Label));

        return favorites;
    }

    private static TunerDto CreateTunerDto(TransportInfoContainer transportInfo)
    {
        return new TunerDto(
            transportInfo.Id,
            transportInfo.Label,
            transportInfo.Manufacturer,
            transportInfo.Model,
            transportInfo.Icon,
            transportInfo.IsOnline,
            transportInfo.SupportsColors,
            transportInfo.SupportsDiscretePower,
            transportInfo.Tags,
            CreateTunerFavoritesList(transportInfo.Favorites)
        );
    }
}