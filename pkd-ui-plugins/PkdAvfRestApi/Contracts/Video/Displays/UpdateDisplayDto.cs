using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Video.Displays;

public record UpdateDisplayDto(
    [Required] bool IsFrozen,
    [Required] bool IsBlank,
    [Required] bool PowerState,
    [Required] string Input
);