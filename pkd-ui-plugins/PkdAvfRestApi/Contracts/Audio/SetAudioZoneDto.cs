using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Audio;

internal record SetAudioZoneDto(
    [Required] string Input,
    [Required] string Zone,
    [Required] bool Enable
);