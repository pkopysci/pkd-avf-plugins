namespace PkdAvfRestApi.Contracts.Video.Routing;

internal record VideoInputDto(
    string Id,
    string Label,
    string Icon,
    bool HasSync,
    bool SupportsSync,
    List<string> Tags
);