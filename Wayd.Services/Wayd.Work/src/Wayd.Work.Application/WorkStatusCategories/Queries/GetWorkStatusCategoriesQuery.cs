using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Application.WorkStatusCategories.Queries;

public sealed record GetWorkStatusCategoriesQuery : IQuery<IReadOnlyList<WorkStatusCategoryListDto>> { }

public sealed class GetWorkStatusCategoriesQueryHandler : IQueryHandler<GetWorkStatusCategoriesQuery, IReadOnlyList<WorkStatusCategoryListDto>>
{
    public Task<IReadOnlyList<WorkStatusCategoryListDto>> Handle(GetWorkStatusCategoriesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkStatusCategoryListDto> values = Enum.GetValues<WorkStatusCategory>().Select(c => new WorkStatusCategoryListDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
