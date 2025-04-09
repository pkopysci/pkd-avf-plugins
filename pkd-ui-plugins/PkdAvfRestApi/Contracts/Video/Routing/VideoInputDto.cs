namespace PkdAvfRestApi.Contracts.Video.Routing;

internal record VideoInputDto(
    string Id,
    string Label,
    string Icon,
    bool HasSync,
    List<string> Tags
);