namespace PkdAvfRestApi.Contracts.Video.VideoWall;

internal record VideoWallLayoutDto(
    string Id,
    string Label,
    string VideoWallControlId,
    int Width,
    int Height,
    List<VideoWallCellDto> Cells
);