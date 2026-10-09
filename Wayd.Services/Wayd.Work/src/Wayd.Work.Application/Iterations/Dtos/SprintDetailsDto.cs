using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Planning;
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
    /// The sprint's state now, worked out from its actual and default dates when read.
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
    /// When the sprint became Active: <see cref="Started"/>, or by default the start of its first planned day
    /// in <see cref="TimeZone"/>, never before the previous sprint ended. <see cref="State"/> is Future before
    /// it. Null for a sprint whose team is not mapped or that has no planned dates.
    /// </summary>
    public Instant? ActiveFrom { get; set; }

    /// <summary>
    /// When the sprint stops being Active, exclusive: <see cref="Completed"/>, or by default the end of its last
    /// planned day, cut to the next sprint's start where the two overlap. A sprint that runs to the end of a day
    /// ends at the next midnight, so its last day is the one before.
    /// </summary>
    public Instant? ActiveUntil { get; set; }

    /// <summary>
    /// The IANA time zone the sprint's planned days are counted in: its team's on the planned start.
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// Days within the sprint the whole team is off beyond its working week and holiday calendar, such as an
    /// offsite, in date order. Those managing the sprint (<see cref="CanManageSprint"/>) can change them.
    /// </summary>
    public IReadOnlyList<LocalDate> TeamDaysOff { get; set; } = [];

    /// <summary>
    /// Whether the sprint is comparable with the team's other sprints. A non-standard sprint is marked to be left
    /// out when comparing the team's sprints. Worked out when read: see <see cref="SprintTypeSource"/>.
    /// </summary>
    public SprintType SprintType { get; set; }

    /// <summary>
    /// Where <see cref="SprintType"/> came from: the team, the mapped planning interval iteration's category,
    /// or the default. Those managing the sprint (<see cref="CanManageSprint"/>) can set or clear the team's.
    /// </summary>
    public SprintTypeSource SprintTypeSource { get; set; }

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

    /// <summary>
    /// The moments the sprint can be recorded as started, now or earlier. Null when it can't be started.
    /// </summary>
    public InstantWindowDto? StartWindow { get; set; }

    /// <summary>Whether the sprint's lifecycle allows completing it now, whoever asks.</summary>
    public bool CanComplete { get; set; }

    /// <summary>
    /// The moments the sprint can be recorded as completed, now or earlier. Null when it can't be completed.
    /// </summary>
    public InstantWindowDto? CompleteWindow { get; set; }

    /// <summary>Whether the sprint's lifecycle allows reopening it now, whoever asks.</summary>
    public bool CanReopen { get; set; }

    /// <summary>
    /// Another sprint of the team that is still open, which starting this one completes.
    /// </summary>
    public NavigationDto? OpenSprint { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Iteration, SprintDetailsDto>()
            .Ignore(dest => dest.State)
            .Map(dest => dest.Start, src => src.DateRange.Start)
            .Map(dest => dest.End, src => src.DateRange.End)
            .Map(dest => dest.Team, src => src.Team)
            .Ignore(dest => dest.ActiveFrom)
            .Ignore(dest => dest.ActiveUntil)
            .Ignore(dest => dest.TimeZone)
            .Ignore(dest => dest.SprintType)
            .Ignore(dest => dest.SprintTypeSource)
            .Ignore(dest => dest.OverlapsPreviousSprint)
            .Ignore(dest => dest.OverlapsNextSprint)
            .Ignore(dest => dest.CanManageSprint)
            .Ignore(dest => dest.CanStart)
            .Ignore(dest => dest.StartWindow!)
            .Ignore(dest => dest.CanComplete)
            .Ignore(dest => dest.CompleteWindow!)
            .Ignore(dest => dest.CanReopen)
            .Ignore(dest => dest.OpenSprint!);
    }
}
