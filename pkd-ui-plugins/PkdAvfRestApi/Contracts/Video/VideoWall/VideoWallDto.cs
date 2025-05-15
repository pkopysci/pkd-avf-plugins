using PkdAvfRestApi.Contracts.Video.Routing;

namespace PkdAvfRestApi.Contracts.Video.VideoWall;

internal record VideoWallDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    bool IsOnline,
    List<string> Tags,
    List<VideoWallCanvasDto> Canvases,
    List<VideoInputDto> Sources
);