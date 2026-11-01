using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Application.WorkStatusCategories.Queries;

/// <summary>The status categories, described as they apply to work items.</summary>
public sealed record GetWorkStatusCategoriesQuery : IQuery<IReadOnlyList<WorkStatusCategoryListDto>> { }

/// <summary>Lists every <see cref="StatusCategory"/> with what it means for a work item.</summary>
public sealed class GetWorkStatusCategoriesQueryHandler : IQueryHandler<GetWorkStatusCategoriesQuery, IReadOnlyList<WorkStatusCategoryListDto>>
{
    // The enum's own descriptions are written for any record; this lookup and its MCP tool speak about work.
    private static readonly Dictionary<StatusCategory, string> WorkDescriptions = new()
    {
        [StatusCategory.Proposed] = "The work has been proposed but not yet started.",
        [StatusCategory.Active] = "The work is currently being performed.",
        [StatusCategory.Done] = "The work has been completed.",
        [StatusCategory.Removed] = "The work has been removed from the backlog without being completed.",
    };

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkStatusCategoryListDto>> Handle(GetWorkStatusCategoriesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkStatusCategoryListDto> values = Enum.GetValues<StatusCategory>().Select(c => new WorkStatusCategoryListDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = WorkDescriptions[c],
            Order = c.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
