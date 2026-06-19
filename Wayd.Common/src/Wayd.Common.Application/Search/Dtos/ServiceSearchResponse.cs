namespace Wayd.Common.Application.Search.Dtos;

public sealed record ServiceSearchRequest(string SearchTerm, int MaxResultsPerCategory);

public sealed record ServiceSearchResponse
{
    public required IReadOnlyList<GlobalSearchCategoryDto> Categories { get; init; }
}
