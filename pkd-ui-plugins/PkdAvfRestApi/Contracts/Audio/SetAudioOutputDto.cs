using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Audio;

internal record SetAudioOutputDto(
    [Required] bool Mute,
    [Required] int Level
);