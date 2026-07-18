using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.TeamTypes.Dtos;

namespace Wayd.Organization.Application.TeamTypes.Queries;

public sealed record GetTeamTypesQuery : IQuery<IReadOnlyList<TeamTypeDto>> { }

public sealed class GetTeamTypesQueryHandler : IQueryHandler<GetTeamTypesQuery, IReadOnlyList<TeamTypeDto>>
{
    public Task<IReadOnlyList<TeamTypeDto>> Handle(GetTeamTypesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<TeamTypeDto> values = Enum.GetValues<TeamType>().Select(t => new TeamTypeDto
        {
            Id = (int)t,
            Name = t.GetDisplayName(),
            Description = t.GetDisplayDescription(),
            Order = t.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
