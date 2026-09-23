namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record ForecastHistogramBucketDto
{
    public required LocalDate Date { get; init; }
    public required int Trials { get; init; }
}
