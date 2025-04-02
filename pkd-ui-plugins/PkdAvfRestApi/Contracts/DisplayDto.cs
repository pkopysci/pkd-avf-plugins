using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts;

public record class DisplayDto(
    int Id,
    string Label,
    string Manufacturer,
    string Model,
    bool PowerState,
    bool HasScreen,
    bool IsOnline,
    List<string> Tags);