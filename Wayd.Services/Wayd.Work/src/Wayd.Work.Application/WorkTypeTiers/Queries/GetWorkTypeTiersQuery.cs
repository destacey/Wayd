using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.WorkTypeTiers.Dtos;

namespace Wayd.Work.Application.WorkTypeTiers.Queries;

public sealed record GetWorkTypeTiersQuery : IQuery<IReadOnlyList<WorkTypeTierDto>> { }

public sealed class GetWorkTypeTiersQueryHandler : IQueryHandler<GetWorkTypeTiersQuery, IReadOnlyList<WorkTypeTierDto>>
{
    public Task<IReadOnlyList<WorkTypeTierDto>> Handle(GetWorkTypeTiersQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkTypeTierDto> values = Enum.GetValues<WorkTypeTier>().Select(c => new WorkTypeTierDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
