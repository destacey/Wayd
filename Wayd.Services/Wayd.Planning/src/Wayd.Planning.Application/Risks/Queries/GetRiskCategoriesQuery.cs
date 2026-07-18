using Wayd.Common.Extensions;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Queries;

public sealed record GetRiskCategoriesQuery : IQuery<IReadOnlyList<RiskCategoryDto>> { }

public sealed class GetRiskCategoriesQueryHandler : IQueryHandler<GetRiskCategoriesQuery, IReadOnlyList<RiskCategoryDto>>
{
    public Task<IReadOnlyList<RiskCategoryDto>> Handle(GetRiskCategoriesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RiskCategoryDto> values = Enum.GetValues<RiskCategory>().Select(c => new RiskCategoryDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
