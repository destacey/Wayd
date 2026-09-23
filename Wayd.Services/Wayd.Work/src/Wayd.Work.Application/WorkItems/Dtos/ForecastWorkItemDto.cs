namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record ForecastWorkItemDto
{
    public required Guid Id { get; init; }
    public required string Key { get; init; }
    public required string WorkspaceKey { get; init; }
    public required string Title { get; init; }
}
