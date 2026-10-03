using Wayd.Common.Application.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Application.Iterations.Dtos;

public sealed record SprintDetailsDto : IMapFrom<Iteration>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The unique key of the sprint.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the sprint.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The current state of the sprint.
    /// </summary>
    public required SimpleNavigationDto State { get; set; }

    /// <summary>
    /// The first planned day of the sprint.
    /// </summary>
    public LocalDate Start { get; set; }

    /// <summary>
    /// The last planned day of the sprint, included in it.
    /// </summary>
    public LocalDate End { get; set; }

    public required WorkTeamNavigationDto Team { get; set; }

    /// <summary>
    /// When the team started the sprint, if it did.
    /// </summary>
    public Instant? Started { get; set; }

    /// <summary>
    /// When the team completed the sprint, if it did.
    /// </summary>
    public Instant? Completed { get; set; }

    /// <summary>
    /// When the sprint actually started: <see cref="Started"/>, or by default the end of the commitment grace
    /// period after the planned start, in <see cref="TimeZone"/>. Null for a sprint whose team is not mapped
    /// or that has no planned dates.
    /// </summary>
    public Instant? EffectiveStart { get; set; }

    /// <summary>
    /// When the sprint actually ended: <see cref="Completed"/>, or by default the end of the last planned day,
    /// cut to the next sprint's start where the two overlap.
    /// </summary>
    public Instant? EffectiveEnd { get; set; }

    /// <summary>
    /// The IANA time zone the sprint's planned days are counted in: its team's on the planned start.
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// Whether the source system plans this sprint to overlap the team's previous sprint.
    /// </summary>
    public bool OverlapsPreviousSprint { get; set; }

    /// <summary>
    /// Whether the source system plans this sprint to overlap the team's next sprint.
    /// </summary>
    public bool OverlapsNextSprint { get; set; }

    /// <summary>
    /// Whether the caller may start, complete or reopen the sprint: a member of its team or team of teams
    /// holding the permission, or an administrator.
    /// </summary>
    public bool CanManageSprint { get; set; }

    /// <summary>Whether the sprint's lifecycle allows starting it now, whoever asks.</summary>
    public bool CanStart { get; set; }

    /// <summary>Whether the sprint's lifecycle allows completing it now, whoever asks.</summary>
    public bool CanComplete { get; set; }

    /// <summary>Whether the sprint's lifecycle allows reopening it now, whoever asks.</summary>
    public bool CanReopen { get; set; }

    /// <summary>
    /// Another sprint of the team that is still open, which starting this one completes.
    /// </summary>
    public NavigationDto? OpenSprint { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Iteration, SprintDetailsDto>()
            .Map(dest => dest.State, src => SimpleNavigationDto.FromEnum(src.State))
            .Map(dest => dest.Start, src => src.DateRange.Start)
            .Map(dest => dest.End, src => src.DateRange.End)
            .Map(dest => dest.Team, src => src.Team)
            .Ignore(dest => dest.EffectiveStart)
            .Ignore(dest => dest.EffectiveEnd)
            .Ignore(dest => dest.TimeZone)
            .Ignore(dest => dest.OverlapsPreviousSprint)
            .Ignore(dest => dest.OverlapsNextSprint)
            .Ignore(dest => dest.CanManageSprint)
            .Ignore(dest => dest.CanStart)
            .Ignore(dest => dest.CanComplete)
            .Ignore(dest => dest.CanReopen)
            .Ignore(dest => dest.OpenSprint!);
    }
}
