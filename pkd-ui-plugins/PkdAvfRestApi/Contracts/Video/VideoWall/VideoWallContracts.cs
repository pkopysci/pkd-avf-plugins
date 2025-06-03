using pkd_application_service.VideoWallControl;
using Microsoft.AspNetCore.Http;
using pkd_application_service.AvRouting;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts.Video.Routing;

namespace PkdAvfRestApi.Contracts.Video.VideoWall;

internal static class VideoWallContracts
{
    public static IResult GetAllControllers(IVideoWallApp appService)
    {
        try
        {
            List<VideoWallDto> dtos = [];
            foreach (var wall in appService.GetAllVideoWalls())
            {
                dtos.Add(CreateVideoWallDto(wall, appService));
            }

            return Results.Ok(dtos);
        }
        catch (Exception e)
        {
            Logger.Error(e, "AVF REST API - GET Video Walls.");
            return Results.Problem("Internal Server Error");
        }
    }

    public static IResult GetSingleVideoWall(IVideoWallApp appService, string id)
    {
        try
        {
            var found = appService.GetAllVideoWalls().FirstOrDefault(vw => vw.Id == id);
            return found is null ? Results.NotFound() : Results.Ok(CreateVideoWallDto(found, appService));
        }
        catch (Exception e)
        {
            Logger.Error(e, $"AVF REST API - GET Video Wall {id}.");
            return Results.Problem("Internal Server Error");
        }
    }

    public static IResult GetAllWallCanvases(IVideoWallApp appService, string id)
    {
        try
        {
            var wallControl = appService.GetAllVideoWalls().FirstOrDefault(vw => vw.Id == id);
            if (wallControl is null) return Results.NotFound();
            
            List<VideoWallCanvasDto> dtos = [];
            foreach (var canvas in wallControl.Canvases)
            {
                dtos.Add(CreateVideoWallCanvasDto(wallControl.Id, canvas, appService));
            }
            return Results.Ok(dtos);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"AVF REST API - GET all video wall {id} canvases.");
            return Results.Problem("Internal Server Error");
        }
    }

    public static IResult GetSingleVideoWallCanvas(IVideoWallApp appService, string wallId, string canvasId)
    {
        try
        {
            var controller = appService.GetAllVideoWalls().FirstOrDefault(vw => vw.Id == wallId);
            if (controller is null) return Results.NotFound("Controller not found");

            var canvas = controller.Canvases.FirstOrDefault(can => can.Id.Equals(canvasId));
            if (canvas is null) return Results.NotFound("Canvas not found");
            
            return Results.Ok(CreateVideoWallCanvasDto(wallId, canvas, appService));
        }
        catch (Exception e)
        {
            Logger.Error(e, $"AVF REST API - GET canvas {canvasId} from {wallId}.");
            return Results.Problem("Internal Server Error");
        }
    }

    public static IResult GetCanvasLayouts(IVideoWallApp appService, string wallId, string canvasId)
    {
        try
        {
            var controller = appService.GetAllVideoWalls().FirstOrDefault(vw => vw.Id == wallId);
            if (controller is null) return Results.NotFound("Controller not found");

            var canvas = controller.Canvases.FirstOrDefault(can => can.Id.Equals(canvasId));
            if (canvas is null) return Results.NotFound("Canvas not found");
            
            List<VideoWallLayoutDto> dtos = [];
            foreach (var layout in canvas.Layouts)
            {
                dtos.Add(CreateVideoWallLayoutDto(layout));
            }
            return Results.Ok(dtos);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"AVF REST API - GET layouts for {canvasId} from {wallId}.");
            return Results.Problem("Internal Server Error");
        }
    }

    private static VideoWallDto CreateVideoWallDto(VideoWallInfoContainer videoWall, IVideoWallApp appService)
    {
        List<VideoWallCanvasDto> canvases = [];
        foreach (var canvas in videoWall.Canvases)
            canvases.Add(CreateVideoWallCanvasDto(videoWall.Id, canvas, appService));

        List<VideoInputDto> sources = [];
        foreach (var input in videoWall.Sources)
            sources.Add(CreateVideoWallSourceDto(input));

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

    private static VideoWallCellDto CreateVideoWallCellDto(VideoWallCellInfo cellInfo)
    {
        return new VideoWallCellDto(
            Id: cellInfo.Id,
            XPosition: cellInfo.XPosition,
            YPosition: cellInfo.YPosition,
            SourceId: cellInfo.SourceId
        );
    }

    private static VideoWallLayoutDto CreateVideoWallLayoutDto(VideoWallLayoutInfo layoutInfo)
    {
        List<VideoWallCellDto> cells = [];
        foreach (var cell in layoutInfo.Cells) cells.Add(CreateVideoWallCellDto(cell));
        return new VideoWallLayoutDto(
            Id: layoutInfo.Id,
            Label: layoutInfo.Label,
            Cells: cells,
            VideoWallControlId: layoutInfo.VideoWallControlId,
            Width: layoutInfo.Width,
            Height: layoutInfo.Height
        );
    }

    private static VideoWallCanvasDto CreateVideoWallCanvasDto(
        string controllerId,
        VideoWallCanvasInfo canvasInfo,
        IVideoWallApp appService)
    {
        List<VideoWallLayoutDto> layouts = [];
        foreach (var layout in canvasInfo.Layouts) layouts.Add(CreateVideoWallLayoutDto(layout));
        return new VideoWallCanvasDto(
            canvasInfo.Id,
            canvasInfo.Label,
            appService.QueryActiveVideoWallLayout(controllerId, canvasInfo.Id),
            [],
            layouts);
    }

    private static VideoInputDto CreateVideoWallSourceDto(AvSourceInfoContainer sourceInfo)
    {
        return new VideoInputDto(
            Id: sourceInfo.Id,
            Label: sourceInfo.Label,
            Icon: sourceInfo.Icon,
            HasSync: true, // TODO: Update this when HasSync is supported
            Tags: sourceInfo.Tags
        );
    }
}