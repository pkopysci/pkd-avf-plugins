using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_application_service.VideoWallControl;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Video.VideoWall;

// ReSharper disable SuspiciousTypeConversion.Global

namespace PkdAvfRestApi.Endpoints;

internal static class VideoWallEndpoints
{
    private static IVideoWallApp? _appService;

    public static RouteGroupBuilder MapVideoWallEndpoints(this WebApplication app, IApplicationService appService)
    {
        _appService = appService as IVideoWallApp;
        var group = app.MapGroup("video/videoWalls");

        group.MapGet("/supported", () => Results.Ok(new SupportedDto(_appService != null)));

        group.MapGet("/",
            () => _appService == null
                ? Results.BadRequest("Video walls not supported.")
                : VideoWallContracts.GetAllControllers(_appService));

        group.MapGet("/{id}",
            (string id) => _appService == null
                ? Results.BadRequest("Video walls not supported.")
                : VideoWallContracts.GetSingleVideoWall(_appService, id));

        group.MapGet("/{id}/canvases",
            (string id) => _appService == null
                ? Results.BadRequest("Video walls not supported.")
                : VideoWallContracts.GetAllWallCanvases(_appService, id));

        group.MapGet("/{id}/canvases/{canvasId}",
            (string id, string canvasId) => _appService == null
                ? Results.BadRequest("Video walls not supported.")
                : VideoWallContracts.GetSingleVideoWallCanvas(_appService, id, canvasId));

        group.MapGet("/{id}/canvases/{canvasId}/layouts",
            (string id, string canvasId) => _appService == null
                ? Results.BadRequest("Video walls not supported.")
                : VideoWallContracts.GetCanvasLayouts(_appService, id, canvasId));

        group.MapPut("/layout", (SetVideoWallLayoutDto data) =>
        {
            if (_appService == null) return Results.BadRequest("Video walls not supported.");
            try
            {
                if (string.IsNullOrEmpty(data.VideoWall) || string.IsNullOrEmpty(data.Layout))
                {
                    return Results.BadRequest("Video Wall or Layout is empty or missing.");
                }

                var wall = _appService.GetAllVideoWalls().FirstOrDefault(vw => vw.Id == data.VideoWall);
                if (wall == null) return Results.NotFound($"Video Wall with id {data.VideoWall} not found.");

                var canvas = wall.Canvases.FirstOrDefault(c => c.Id.Equals(data.Canvas));
                if (canvas == null)
                    return Results.NotFound($"Video Wall with id {data.VideoWall} - canvas {data.Canvas} not found.");

                var layout = canvas.Layouts.FirstOrDefault(l => l.Id == data.Layout);
                if (layout == null)
                    return Results.NotFound(
                        $"Layout id {data.Layout} not found for {data.VideoWall} - canvas {data.Canvas}.");

                _appService.SetActiveVideoWallLayout(data.VideoWall, data.Canvas, data.Layout);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT Video Wall.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/route", (SetCellRouteDto data) =>
        {
            if (_appService == null) return Results.BadRequest("Video Walls not supported.");
            try
            {
                if (string.IsNullOrEmpty(data.VideoWall) || string.IsNullOrEmpty(data.Cell) ||
                    string.IsNullOrEmpty(data.Input))
                {
                    return Results.BadRequest("VideoWall, Cell, or Input is empty or missing.");
                }

                var wall = _appService.GetAllVideoWalls().FirstOrDefault(vw => vw.Id == data.VideoWall);
                if (wall == null) return Results.NotFound($"VideoWall with id {data.VideoWall} not found.");

                var canvas = wall.Canvases.FirstOrDefault(c => c.Id.Equals(data.Canvas));
                if (canvas == null)
                    return Results.NotFound($"Video Wall {data.VideoWall} - canvas {data.Canvas} not found.");

                var activeLayout = _appService.QueryActiveVideoWallLayout(wall.Id, canvas.Id);
                var layout = canvas.Layouts.FirstOrDefault(layout => layout.Id == activeLayout);
                if (layout?.Cells.FindIndex(x => x.Id == data.Cell) < 0)
                {
                    return Results.NotFound(
                        $"Video Wall layout {activeLayout} doesn't contain a cell with id {data.Cell}.");
                }

                if (wall.Sources.FirstOrDefault(x => x.Id == data.Input) == null)
                {
                    return Results.NotFound($"Video Wall Does not have an input source with id {data.Input}.");
                }

                _appService.SetVideoWallCellRoute(wall.Id, canvas.Id, data.Cell, data.Input);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT video wall cell route.");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }
}