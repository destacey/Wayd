using Wayd.Common.Extensions;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectStatusesQuery : IQuery<IReadOnlyList<ProjectStatusDto>> { }

public sealed class GetProjectStatusesQueryHandler : IQueryHandler<GetProjectStatusesQuery, IReadOnlyList<ProjectStatusDto>>
{
    public Task<IReadOnlyList<ProjectStatusDto>> Handle(GetProjectStatusesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectStatusDto> values = [.. Enum.GetValues<ProjectStatus>().Select(c => new ProjectStatusDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder(),
            LifecyclePhase = c.GetDisplayGroupName() ?? "Unknown"
        })];

        return Task.FromResult(values);
    }
}
