using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed record GetSprintTypesQuery : IQuery<List<SprintTypeDto>>;

public sealed class GetSprintTypesQueryHandler : IQueryHandler<GetSprintTypesQuery, List<SprintTypeDto>>
{
    public Task<List<SprintTypeDto>> Handle(GetSprintTypesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommonEnumDto<SprintType>.GetValues<SprintTypeDto>());
    }
}

/// <summary>
/// A sprint type, with the code a sprint carries and the request to set one accepts.
/// </summary>
public sealed record SprintTypeDto : CommonEnumDto<SprintType>;
