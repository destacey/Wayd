using Wayd.Common.Extensions;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;

public sealed record GetStrategicInitiativeStatusesQuery : IQuery<IReadOnlyList<StrategicInitiativeStatusDto>> { }

public sealed class GetStrategicInitiativeStatusesQueryHandler : IQueryHandler<GetStrategicInitiativeStatusesQuery, IReadOnlyList<StrategicInitiativeStatusDto>>
{
    public Task<IReadOnlyList<StrategicInitiativeStatusDto>> Handle(GetStrategicInitiativeStatusesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<StrategicInitiativeStatusDto> values = [.. Enum.GetValues<StrategicInitiativeStatus>().Select(c => new StrategicInitiativeStatusDto
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
