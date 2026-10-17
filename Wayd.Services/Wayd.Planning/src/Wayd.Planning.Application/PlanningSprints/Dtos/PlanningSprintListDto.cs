using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Models;

namespace Wayd.Planning.Application.PlanningSprints.Dtos;

/// <summary>
/// A sprint mapped to a planning interval, as listed in planning views.
/// </summary>
public sealed record PlanningSprintListDto : IMapFrom<PlanningSprint>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The sprint's numeric key, an alternate to its id.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the sprint.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The sprint's current state.
    /// </summary>
    /// <remarks>
    /// The Planning copy does not hold it; the query that returns this DTO reads it from the Work module.
    /// </remarks>
    public required SimpleNavigationDto State { get; set; }

    /// <summary>
    /// The first planned day of the sprint.
    /// </summary>
    public LocalDate Start { get; set; }

    /// <summary>
    /// The last planned day of the sprint, included in it.
    /// </summary>
    public LocalDate End { get; set; }

    /// <summary>
    /// When the sprint became Active. Null for a sprint whose team is not mapped.
    /// </summary>
    /// <remarks>
    /// Read from the Work module, like <see cref="State"/>.
    /// </remarks>
    public Instant? ActiveFrom { get; set; }

    /// <summary>When the sprint stops being Active, exclusive.</summary>
    public Instant? ActiveUntil { get; set; }

    /// <summary>The IANA time zone the sprint's days are counted in: its team's.</summary>
    public string? TimeZone { get; set; }

    public required PlanningTeamNavigationDto Team { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PlanningSprint, PlanningSprintListDto>()
            .Ignore(dest => dest.State)
            .Ignore(dest => dest.ActiveFrom)
            .Ignore(dest => dest.ActiveUntil)
            .Ignore(dest => dest.TimeZone!)
            .Map(dest => dest.Start, src => src.DateRange.Start)
            .Map(dest => dest.End, src => src.DateRange.End)
            .Map(dest => dest.Team, src => PlanningTeamNavigationDto.FromPlanningTeam(src.Team!));
    }
}
