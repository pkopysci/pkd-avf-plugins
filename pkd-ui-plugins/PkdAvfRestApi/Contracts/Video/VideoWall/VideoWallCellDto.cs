namespace PkdAvfRestApi.Contracts.Video.VideoWall;

internal record VideoWallCellDto(
    string Id,
    int XPosition,
    int YPosition,
    string SourceId
);