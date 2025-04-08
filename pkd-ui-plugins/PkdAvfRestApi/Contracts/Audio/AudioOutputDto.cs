namespace PkdAvfRestApi.Contracts.Audio;

internal record AudioOutputDto(
    string Id,
    string Label,
    string Icon,
    string RoutedInput,
    bool Mute,
    int Level,
    List<string> Tags
);