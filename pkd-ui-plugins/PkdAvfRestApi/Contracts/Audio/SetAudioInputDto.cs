using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Audio;

internal record SetAudioInputDto(
    [Required] bool Mute,
    [Required] int Level
);