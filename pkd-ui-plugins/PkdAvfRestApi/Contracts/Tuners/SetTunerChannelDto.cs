using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Tuners;

internal record SetTunerChannelDto(
    [Required] string Channel
);