using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Lighting;

internal record SetLightingLoadDto(
    [Required] string ZoneId,
    [Required][Range(0,100)] int Load
);