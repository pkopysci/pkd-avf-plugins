namespace PkdAvfRestApi.Contracts.Tuners;

internal record TunerDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    string Icon,
    bool IsOnline,
    bool SupportsColorButtons,
    bool SupportsDiscretePower,
    List<string> Tags,
    List<TunerFavoriteDto> Favorites
);