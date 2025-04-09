using PkdAvfRestApi.Contracts.Video.Routing;

namespace PkdAvfRestApi.Contracts.Video.VideoWall;

internal record VideoWallDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    string ActiveLayout,
    bool IsOnline,
    List<string> Tags,
    List<VideoWallLayoutDto> Layouts,
    List<VideoInputDto> Sources
);