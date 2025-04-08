using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Audio;

internal record SetAudioRouteDto(
    [Required] string Input,
    [Required] string Output
);