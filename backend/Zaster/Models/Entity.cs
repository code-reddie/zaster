using System.ComponentModel.DataAnnotations;

namespace Zaster.Models;

public record Entity
{
    [Key]
    public int Id { get; init; }
}