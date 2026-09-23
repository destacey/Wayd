namespace Wayd.Work.Application.WorkTeams.Dtos;

public sealed record ThroughputHistogramBucketDto
{
    public required int WorkItems { get; init; }
    public required int Trials { get; init; }
}
