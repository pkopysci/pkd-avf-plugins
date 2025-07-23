using System.ComponentModel.DataAnnotations;
namespace PkdAvfRestApi.Contracts.Audio;

internal record SetAudioLevelDto([Required]int Level);