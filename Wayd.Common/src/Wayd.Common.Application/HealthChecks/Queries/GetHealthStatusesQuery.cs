using Wayd.Common.Application.HealthChecks.Dtos;
using Wayd.Common.Domain.Enums;

namespace Wayd.Common.Application.HealthChecks.Queries;

public sealed record GetHealthStatusesQuery : IQuery<IReadOnlyList<HealthStatusDto>>;

public sealed class GetHealthStatusesQueryHandler : IQueryHandler<GetHealthStatusesQuery, IReadOnlyList<HealthStatusDto>>
{
    public Task<IReadOnlyList<HealthStatusDto>> Handle(GetHealthStatusesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<HealthStatusDto> values = [.. Enum.GetValues<HealthStatus>().Select(c => new HealthStatusDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder()
        })];

        return Task.FromResult(values);
    }
}
