using System.ComponentModel.DataAnnotations;

namespace Zaster.Models;

public record Entity
{
    [Key]
    public required int Id { get; init; }
}
