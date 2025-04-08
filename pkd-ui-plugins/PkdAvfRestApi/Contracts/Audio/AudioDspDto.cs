namespace PkdAvfRestApi.Contracts.Audio;

internal record AudioDspDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    string Icon,
    bool IsOnline,
    List<string> Tags
);