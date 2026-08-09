using System.ComponentModel.DataAnnotations;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

public sealed record ProjectStatusDto
{
    public int Id { get; set; }

    [Required]
    public required string Name { get; set; }

    public string? Description { get; set; }

    public int Order { get; set; }

    [Required]
    public required string LifecycleCategory { get; set; }
}
