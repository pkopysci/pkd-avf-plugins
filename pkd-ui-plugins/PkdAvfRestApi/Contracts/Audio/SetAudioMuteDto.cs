using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Audio;

public record SetAudioMuteDto([Required] bool Mute);