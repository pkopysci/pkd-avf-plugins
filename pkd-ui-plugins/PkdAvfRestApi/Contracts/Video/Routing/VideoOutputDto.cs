namespace PkdAvfRestApi.Contracts.Video.Routing;

internal record VideoOutputDto(
    string Id,
    string Label,
    string Icon,
    string CurrentSource,
    List<string> Tags
);