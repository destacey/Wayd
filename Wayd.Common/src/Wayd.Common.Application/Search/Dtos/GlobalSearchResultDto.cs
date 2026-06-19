namespace Wayd.Common.Application.Search.Dtos;

public sealed record GlobalSearchResultDto
{
    public required IReadOnlyList<GlobalSearchCategoryDto> Categories { get; init; }
}

public sealed record GlobalSearchCategoryDto
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required IReadOnlyList<GlobalSearchResultItemDto> Items { get; init; }
    public required int TotalCount { get; init; }
}

public sealed record GlobalSearchResultItemDto
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public required string Key { get; init; }
    public required string EntityType { get; init; }
    public string? AuxKey { get; init; }
}
