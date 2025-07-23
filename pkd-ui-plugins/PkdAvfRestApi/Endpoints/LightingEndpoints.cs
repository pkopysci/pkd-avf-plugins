using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_application_service.LightingControl;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Lighting;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable RedundantCast

namespace PkdAvfRestApi.Endpoints;

internal static class LightingEndpoints
{
    private static ILightingControlApp? _appService;

    public static RouteGroupBuilder MapLightingEndpoints(this WebApplication app, IApplicationService appService)
    {
        _appService = appService as ILightingControlApp;


        var group = app.MapGroup("lighting");

        group.MapGet("supported", () =>
            new SupportedDto(_appService != null && _appService.GetAllLightingDeviceInfo().Count > 0));

        group.MapGet("controllers", () =>
        {
            if (_appService == null) return Results.BadRequest("Lighting not supported.");
            try
            {
                List<LightingControllerDto> controllers = [];
                foreach (var control in _appService.GetAllLightingDeviceInfo())
                {
                    controllers.Add(CreateLightingController(control));
                }

                return Results.Ok(controllers);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AVF REST API - GET lighting controllers.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("controllers/{id}", (string id) =>
        {
            if (_appService == null || _appService.GetAllLightingDeviceInfo().Count == 0)
                return Results.BadRequest("Lighting not supported.");
            try
            {
                var found = _appService.GetAllLightingDeviceInfo().FirstOrDefault(x => x.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateLightingController(found));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - GET lighting controller {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/zones/{controlId}", (string controlId) =>
        {
            if (_appService == null || _appService.GetAllLightingDeviceInfo().Count == 0)
                return Results.BadRequest("Lighting not supported.");
            try
            {
                var controller = _appService.GetAllLightingDeviceInfo().FirstOrDefault(x => x.Id == controlId);
                if (controller == null) return Results.NotFound("ControlId");

                List<LightingZoneDto> zones = [];
                foreach (var zone in controller.Zones)
                {
                    zones.Add(CreateLightingZone(controller.Id, zone));
                }

                return Results.Ok(zones);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - GET lighting zones for {controlId}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/zones/{controlId}/{zoneId}", (string controlId, string zoneId) =>
        {
            if (_appService == null || _appService.GetAllLightingDeviceInfo().Count == 0)
                return Results.BadRequest("Lighting not supported.");
            try
            {
                var control = _appService.GetAllLightingDeviceInfo().FirstOrDefault(x => x.Id == controlId);
                if (control == null) return Results.NotFound("ControlId");

                var zone = control.Zones.FirstOrDefault(x => x.Id == zoneId);
                return zone == null ? Results.NotFound("ZoneId") : Results.Ok(CreateLightingZone(control.Id, zone));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - GET lighting zone {zoneId} for {controlId}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/scenes/{controlId}", (string controlId) =>
        {
            if (_appService == null || _appService.GetAllLightingDeviceInfo().Count == 0)
                return Results.BadRequest("Lighting not supported.");
            try
            {
                var control = _appService.GetAllLightingDeviceInfo().FirstOrDefault(x => x.Id == controlId);
                if (control == null) return Results.NotFound("ControlId");

                List<LightingSceneDto> scenes = [];
                foreach (var scene in control.Scenes)
                {
                    scenes.Add(CreateLightingScene(control.Id, scene));
                }

                return Results.Ok(scenes);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - GET lighting Scenes for {controlId}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/scenes/{controlId}/{sceneId}", (string controlId, string sceneId) =>
        {
            if (_appService == null || _appService.GetAllLightingDeviceInfo().Count == 0)
                return Results.BadRequest("Lighting not supported.");
            try
            {
                var control = _appService.GetAllLightingDeviceInfo().FirstOrDefault(x => x.Id == controlId);
                if (control == null) return Results.NotFound("ControlId");

                var scene = control.Scenes.FirstOrDefault(x => x.Id == sceneId);
                return scene == null ? Results.NotFound("SceneId") : Results.Ok(CreateLightingScene(control.Id, scene));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - GET lighting Scene {sceneId} for {controlId}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/zones/{controlId}", (string controlId, SetLightingLoadDto body) =>
        {
            if (_appService == null || _appService.GetAllLightingDeviceInfo().Count == 0)
                return Results.BadRequest("Lighting not supported.");
            try
            {
                if (string.IsNullOrEmpty(body.ZoneId))
                {
                    return Results.BadRequest("Missing ZoneId.");
                }

                if (body.Load is < 0 or > 100)
                {
                    return Results.BadRequest("Invalid Load. range is 0-100.");
                }

                var control = _appService.GetAllLightingDeviceInfo().FirstOrDefault(x => x.Id == controlId);
                if (control == null) return Results.NotFound("ControlId");

                var zone = control.Zones.FirstOrDefault(x => x.Id == body.ZoneId);
                if (zone == null) return Results.NotFound("ZoneId");

                _appService.SetLightingLoad(controlId, body.ZoneId, body.Load);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - PUT lighting zone for {controlId}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("scenes/{controlId}", (string controlId, SetLightingSceneDto body) =>
        {
            if (_appService == null || _appService.GetAllLightingDeviceInfo().Count == 0)
                return Results.BadRequest("Lighting not supported.");
            try
            {
                if (string.IsNullOrEmpty(body.SceneId))
                {
                    return Results.BadRequest("Missing SceneId.");
                }

                var control = _appService.GetAllLightingDeviceInfo().FirstOrDefault(x => x.Id == controlId);
                if (control == null) return Results.NotFound("ControlId");

                var zone = control.Scenes.FirstOrDefault(x => x.Id == body.SceneId);
                if (zone == null) return Results.NotFound("SceneId");

                _appService.RecallLightingScene(controlId, body.SceneId);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - PUT lighting scene for {controlId}.");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }

    private static LightingControllerDto CreateLightingController(LightingControlInfoContainer control)
    {
        List<LightingSceneDto> scenes = [];
        foreach (var scene in control.Scenes)
            scenes.Add(CreateLightingScene(control.Id, scene));

        List<LightingZoneDto> zones = [];
        foreach (var zone in control.Zones)
            zones.Add(CreateLightingZone(control.Id, zone));

        return new LightingControllerDto(
            Id: control.Id,
            Label: control.Label,
            Icon: control.Icon,
            Manufacturer: control.Manufacturer,
            Model: control.Model,
            IsOnline: control.IsOnline,
            Tags: control.Tags,
            Zones: zones,
            Scenes: scenes
        );
    }

    private static LightingSceneDto CreateLightingScene(string controlId, LightingItemInfoContainer scene)
    {
        return new LightingSceneDto(
            Id: scene.Id,
            Label: scene.Label,
            Icon: scene.Icon,
            Index: scene.Index,
            IsActive: _appService != null && _appService.GetActiveScene(controlId) == scene.Id,
            Tags: scene.Tags
        );
    }

    private static LightingZoneDto CreateLightingZone(string controlId, LightingItemInfoContainer zone)
    {
        return new LightingZoneDto(
            Id: zone.Id,
            Label: zone.Label,
            Icon: zone.Icon,
            Index: zone.Index,
            Load: _appService?.GetZoneLoad(controlId, zone.Id) ?? 0,
            Tags: zone.Tags
        );
    }
}