namespace PkdAvfRestApi.Contracts.Video.VideoWall;

internal record VideoWallCanvasDto(
    string Id,
    string Label,
    string ActiveLayout,
    List<string> Tags,
    List<VideoWallLayoutDto> Layouts
    );