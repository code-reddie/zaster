using System.ComponentModel.DataAnnotations;

namespace Zaster.Database;

public record Entity
{
    [Key]
    public int Id { get; init; }
}