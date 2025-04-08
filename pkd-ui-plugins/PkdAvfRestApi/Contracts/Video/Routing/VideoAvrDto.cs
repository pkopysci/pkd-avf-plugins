namespace PkdAvfRestApi.Contracts.Video.Routing;

internal record VideoAvrDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    bool IsOnline,
    List<string> Tags
);