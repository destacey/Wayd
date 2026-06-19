using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;

public sealed record ProjectPortfolioStatusDto
{
    public int Id { get; set; }

    [Required]
    public required string Name { get; set; }

    public string? Description { get; set; }

    public int Order { get; set; }

    [Required]
    public required string LifecyclePhase { get; set; }
}
