using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>The reasons time away can be recorded for.</summary>
public sealed record GetTimeAwayReasonsQuery : IQuery<List<TimeAwayReasonDto>>;

public sealed class GetTimeAwayReasonsQueryHandler : IQueryHandler<GetTimeAwayReasonsQuery, List<TimeAwayReasonDto>>
{
    public Task<List<TimeAwayReasonDto>> Handle(GetTimeAwayReasonsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommonEnumDto<TimeAwayReason>.GetValues<TimeAwayReasonDto>());
    }
}
