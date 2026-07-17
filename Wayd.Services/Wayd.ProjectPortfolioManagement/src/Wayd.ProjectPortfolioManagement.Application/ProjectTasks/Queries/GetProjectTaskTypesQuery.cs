using Wayd.Common.Extensions;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;

public sealed record GetProjectTaskTypesQuery : IQuery<IReadOnlyList<ProjectTaskTypeDto>> { }

public sealed class GetProjectTaskTypesQueryHandler : IQueryHandler<GetProjectTaskTypesQuery, IReadOnlyList<ProjectTaskTypeDto>>
{
    public Task<IReadOnlyList<ProjectTaskTypeDto>> Handle(GetProjectTaskTypesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectTaskTypeDto> values = [.. Enum.GetValues<ProjectTaskType>().Select(c => new ProjectTaskTypeDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder()
        })];

        return Task.FromResult(values);
    }
}
