namespace Wayd.Common.Application.Requests.Goals.Dtos;

public sealed record ObjectiveSearchResultDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid? PlanId { get; init; }
    public Guid? OwnerId { get; init; }
}
