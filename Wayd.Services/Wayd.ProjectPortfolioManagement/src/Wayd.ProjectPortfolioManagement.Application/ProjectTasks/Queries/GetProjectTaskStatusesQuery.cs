using Wayd.Common.Extensions;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;

public sealed record GetProjectTaskStatusesQuery : IQuery<IReadOnlyList<TaskStatusDto>> { }

internal sealed class GetProjectTaskStatusesQueryHandler : IQueryHandler<GetProjectTaskStatusesQuery, IReadOnlyList<TaskStatusDto>>
{
    public Task<IReadOnlyList<TaskStatusDto>> Handle(GetProjectTaskStatusesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<TaskStatusDto> values = [.. Enum.GetValues<TaskStatus>().Select(c => new TaskStatusDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder()
        })];

        return Task.FromResult(values);
    }
}
