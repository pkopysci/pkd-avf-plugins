using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.System;

public record UpdateStateDto([Required]bool State);