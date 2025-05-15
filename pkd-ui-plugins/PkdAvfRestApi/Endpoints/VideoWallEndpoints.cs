using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_application_service.VideoWallControl;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Video.Routing;
using PkdAvfRestApi.Contracts.Video.VideoWall;

// ReSharper disable SuspiciousTypeConversion.Global

namespace PkdAvfRestApi.Endpoints;

internal static class VideoWallEndpoints
{
    private static IVideoWallApp? _appService;

    public static RouteGroupBuilder MapVideoWallEndpoints(this WebApplication app, IApplicationService appService)
    {
        _appService = appService as IVideoWallApp;
        if (_appService == null)
        {
            Logger.Warn(
                "AVF REST Api - Video Wall Endpoints - provided IApplicationService does not implement IVideoWallApp.");
        }

        var group = app.MapGroup("video/videoWalls");

        group.MapGet("/supported", () => Results.Ok(new SupportedDto(_appService != null)));

        group.MapGet("/", () =>
        {
            if (_appService == null) return Results.BadRequest("Video walls not supported.");
            try
            {
                List<VideoWallDto> dtos = [];
                foreach (var wall in _appService.GetAllVideoWalls())
                {
                    dtos.Add(CreateVideoWallDto(wall));
                }

                return Results.Ok(dtos);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - GET Video Walls.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Video walls not supported.");
            try
            {
                var found = _appService.GetAllVideoWalls().FirstOrDefault(vw => vw.Id == id);
                return found is null ? Results.NotFound() : Results.Ok(CreateVideoWallDto(found));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET Video Wall {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

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
                if (canvas == null) return Results.NotFound($"Video Wall with id {data.VideoWall} - canvas {data.Canvas} not found.");
                
                var layout = canvas.Layouts.FirstOrDefault(l => l.Id == data.Layout);
                if (layout == null) return Results.NotFound($"Layout id {data.Layout} not found for {data.VideoWall} - canvas {data.Canvas}.");

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
                if (canvas == null) return Results.NotFound($"Video Wall {data.VideoWall} - canvas {data.Canvas} not found.");
                
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

    private static VideoWallDto CreateVideoWallDto(VideoWallInfoContainer videoWall)
    {
        List<VideoWallCanvasDto> canvases = [];
        foreach (var canvas in videoWall.Canvases)
        {
            List<VideoWallLayoutDto> layouts = [];
            foreach (var layout in canvas.Layouts)
            {
                List<VideoWallCellDto> cells = [];
                foreach (var cell in layout.Cells)
                {
                    cells.Add(new VideoWallCellDto(
                        Id: cell.Id,
                        XPosition: cell.XPosition,
                        YPosition: cell.YPosition,
                        SourceId: cell.SourceId
                    ));
                }

                layouts.Add(new VideoWallLayoutDto(
                    Id: layout.Id,
                    Label: layout.Label,
                    Cells: cells,
                    VideoWallControlId: layout.VideoWallControlId,
                    Width: layout.Width,
                    Height: layout.Height
                ));
            }

            canvases.Add(new VideoWallCanvasDto(
                canvas.Id,
                canvas.Label,
                _appService?.QueryActiveVideoWallLayout(videoWall.Id, canvas.Id) ?? string.Empty,
                [],
                layouts));
        }

        List<VideoInputDto> sources = [];
        foreach (var input in videoWall.Sources)
        {
            sources.Add(new VideoInputDto(
                Id: input.Id,
                Label: input.Label,
                Icon: input.Icon,
                HasSync: true, // TODO: Update this when HasSync is supported
                Tags: input.Tags
            ));
        }

        return new VideoWallDto(
            Id: videoWall.Id,
            Label: videoWall.Label,
            Manufacturer: videoWall.Manufacturer,
            Model: videoWall.Model,
            Canvases: canvases,
            Tags: videoWall.Tags,
            IsOnline: videoWall.IsOnline,
            Sources: sources
        );
    }
}