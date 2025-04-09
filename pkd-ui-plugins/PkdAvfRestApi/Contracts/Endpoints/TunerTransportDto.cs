using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Endpoints;

public record TunerTransportDto(
    [Required] string Transport
);