using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Dtos;

public sealed record ProjectLifecyclePhaseDto : IMapFrom<ProjectLifecyclePhase>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Order { get; set; }
}
