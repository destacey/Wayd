using System.ComponentModel.DataAnnotations;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;

public sealed record ProjectPortfolioStatusDto
{
    public int Id { get; set; }

    /// <summary>The status's stable name, as status filters accept it.</summary>
    public ProjectPortfolioStatus Code { get; set; }

    [Required]
    public required string Name { get; set; }

    public string? Description { get; set; }

    public int Order { get; set; }

    [Required]
    public required string LifecycleCategory { get; set; }
}
