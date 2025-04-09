using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Endpoints;

public record SetTunerChannelDto(
    [Required] string Channel
);