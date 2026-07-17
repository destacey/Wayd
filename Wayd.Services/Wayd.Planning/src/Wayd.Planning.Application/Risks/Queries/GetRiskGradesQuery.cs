using Wayd.Common.Extensions;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Queries;

public sealed record GetRiskGradesQuery : IQuery<IReadOnlyList<RiskGradeDto>> { }

public sealed class GetRiskGradesQueryHandler : IQueryHandler<GetRiskGradesQuery, IReadOnlyList<RiskGradeDto>>
{
    public Task<IReadOnlyList<RiskGradeDto>> Handle(GetRiskGradesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RiskGradeDto> values = Enum.GetValues<RiskGrade>().Select(g => new RiskGradeDto
        {
            Id = (int)g,
            Name = g.GetDisplayName(),
            Description = g.GetDisplayDescription(),
            Order = g.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
