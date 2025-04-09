using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_application_service.Base;
using pkd_application_service.CameraControl;
using pkd_common_utils.DataObjects;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Cameras;

// ReSharper disable SuspiciousTypeConversion.Global

namespace PkdAvfRestApi.Endpoints;

internal static class CameraEndpoints
{
    private static ICameraControlApp? _appService;

    public static RouteGroupBuilder MapCameraEndpoints(this WebApplication app, IApplicationService appService)
    {
        _appService = appService as ICameraControlApp;
        if (_appService == null)
        {
            Logger.Warn(
                "AVF REST Api - Video Wall Endpoints - provided IApplicationService does not implement IVideoWallApp.");
        }

        var group = app.MapGroup("endpoints/cameras");

        group.MapGet("/supported", () => Results.Ok(new SupportedDto(_appService != null)));

        group.MapGet("/", () =>
        {
            if (_appService == null) return Results.BadRequest("Camera control not supported.");
            try
            {
                List<CameraDto> body = [];
                foreach (var camera in _appService.GetAllCameraDeviceInfo())
                {
                    body.Add(CreateCameraDto(camera));
                }

                return Results.Ok(body);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AVF REST API - GET cameras.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Camera control not supported.");
            try
            {
                var found = _appService.GetAllCameraDeviceInfo().FirstOrDefault(x => x.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateCameraDto(found));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - GET camera {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/panTilt/{id}", (string id, SetCameraPanTiltDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Camera control not supported.");
            try
            {
                if (body.X is < 0 or > 100) return Results.BadRequest("X must be between 0 and 100.");
                if (body.Y is < 0 or > 100) return Results.BadRequest("Y must be between 0 and 100.");

                var found = _appService.GetAllCameraDeviceInfo().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound(id);

                _appService.SendCameraPanTilt(id, new Vector2D { X = body.X, Y = body.Y });
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - PUT camera pan/tilt {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/zoom/{id}", (string id, SetCameraZoomDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Camera control not supported.");
            try
            {
                if (body.Speed is < 0 or > 100) return Results.BadRequest("Speed must be between 0 and 100.");
                
                var found = _appService.GetAllCameraDeviceInfo().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound(id);
                
                _appService.SendCameraZoom(id, body.Speed);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - PUT camera zoom {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/preset/save/{id}", (string id, SetCameraPresetDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Camera control not supported.");
            try
            {
                if (string.IsNullOrEmpty(body.Preset)) return Results.BadRequest("Preset is missing.");
                var found = _appService.GetAllCameraDeviceInfo().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound(id);
                if (!found.Presets.Exists(x => x.Id == body.Preset)) return Results.NotFound(body.Preset);
                
                _appService.SendCameraPresetSave(id, body.Preset);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - PUT camera {id} save preset.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/preset/recall/{id}", (string id, SetCameraPresetDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Camera control not supported.");
            try
            {
                if (string.IsNullOrEmpty(body.Preset)) return Results.BadRequest("Preset is missing.");
                var found = _appService.GetAllCameraDeviceInfo().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound(id);
                if (!found.Presets.Exists(x => x.Id == body.Preset)) return Results.NotFound(body.Preset);
                
                _appService.SendCameraPresetRecall(id, body.Preset);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - PUT camera {id} recall preset.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/power/{id}", (string id, SetCameraPowerDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Camera control not supported.");
            try
            {
                var found = _appService.GetAllCameraDeviceInfo().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound(id);

                if (_appService.QueryCameraPowerStatus(id) != body.State)
                {
                    _appService.SendCameraPowerChange(id, body.State);
                }
                
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"AVF REST API - PUT camera {id} power.");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }

    private static List<CameraPresetDto> CreatePresetDtoCollection(List<InfoContainer> presets)
    {
        List<CameraPresetDto> data = [];
        foreach (var preset in presets)
        {
            data.Add(new CameraPresetDto(Id: preset.Id, Label: preset.Label));
        }

        return data;
    }

    private static CameraDto CreateCameraDto(CameraInfoContainer camera)
    {
        var presets = CreatePresetDtoCollection(camera.Presets);
        return new CameraDto(
            Id: camera.Id,
            Label: camera.Label,
            Manufacturer: camera.Manufacturer,
            Model: camera.Model,
            Icon: camera.Icon,
            IsOnline: camera.IsOnline,
            SupportsSavingPresets: camera.SupportsSavingPresets,
            SupportsZoom: camera.SupportsZoom,
            SupportsPanTilt: camera.SupportsPanTilt,
            SupportsPower: true, // TODO: Update when app service API allows for it
            PowerState: camera.PowerState,
            Tags: camera.Tags,
            Presets: presets
        );
    }
}