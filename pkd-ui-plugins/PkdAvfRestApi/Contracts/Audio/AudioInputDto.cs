namespace PkdAvfRestApi.Contracts.Audio;

internal record AudioInputDto(
    string Id,
    string Label,
    string Icon,
    bool Mute,
    int Level,
    List<string> Tags,
    List<ZoneControlDto> ZoneControls
);