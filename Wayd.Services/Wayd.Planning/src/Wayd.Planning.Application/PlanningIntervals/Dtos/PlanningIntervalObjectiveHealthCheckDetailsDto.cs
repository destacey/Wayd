using Mapster;
using Wayd.Common.Application.Dtos;
using Wayd.Planning.Domain.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

public sealed record PlanningIntervalObjectiveHealthCheckDetailsDto : IMapFrom<PlanningIntervalObjectiveHealthCheck>
{
    /// <summary>
    /// The id of the health check.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The id of the parent planning interval objective.
    /// </summary>
    public Guid PlanningIntervalObjectiveId { get; set; }

    /// <summary>
    /// The status of the health check.
    /// </summary>
    public required SimpleNavigationDto Status { get; set; }

    /// <summary>
    /// The employee who reported the health check.
    /// </summary>
    public required NavigationDto ReportedBy { get; set; }

    /// <summary>
    /// The timestamp of when the health check was reported.
    /// </summary>
    public Instant ReportedOn { get; set; }

    /// <summary>
    /// The expiration of the health check.
    /// </summary>
    public Instant Expiration { get; set; }

    /// <summary>
    /// The note for the health check.
    /// </summary>
    public string? Note { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PlanningIntervalObjectiveHealthCheck, PlanningIntervalObjectiveHealthCheckDetailsDto>()
            .Map(dest => dest.Status, src => SimpleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.ReportedBy, src => NavigationDto.Create(src.ReportedBy.Id, src.ReportedBy.Key, src.ReportedBy.Name.DisplayName));
    }
}
