using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Extensions;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Application.EstimationScales.Dtos;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

public sealed record PokerSessionDetailsDto : IMapFrom<PokerSession>
{
    private static readonly HashSet<string> _revealedStatuses =
    [
        PokerRoundStatus.Revealed.GetDisplayName(),
        PokerRoundStatus.Accepted.GetDisplayName()
    ];

    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }

    /// <summary>
    /// Public: anyone with the poker View permission can see and join the session. Private: only its
    /// facilitators, invited employees and the members of invited teams.
    /// </summary>
    public Visibility Visibility { get; set; }

    /// <summary>
    /// The employees who run the session: its rounds, reveals, resets, consensus, details, access and completion.
    /// </summary>
    public List<EmployeeNavigationDto> Facilitators { get; set; } = [];

    /// <summary>
    /// The employees invited to the session, who can join it while it is private.
    /// </summary>
    public List<EmployeeNavigationDto> InvitedEmployees { get; set; } = [];

    /// <summary>
    /// The teams invited to the session, whose current members can join it while it is private.
    /// </summary>
    public List<PlanningTeamNavigationDto> InvitedTeams { get; set; } = [];

    /// <summary>
    /// Everyone who has joined the session, in the order they joined, including those who have left.
    /// </summary>
    public List<PokerSessionMemberDto> Members { get; set; } = [];

    /// <summary>
    /// Whether the caller runs the session, as a facilitator or a poker administrator. Always false for a
    /// read-only token.
    /// </summary>
    public bool CanManage { get; set; }
    public EstimationScaleNavigationDto? EstimationScale { get; set; }

    /// <summary>
    /// The scale version the session votes on, which stays put when the scale publishes a newer one.
    /// </summary>
    public EstimationScaleVersionDto? EstimationScaleVersion { get; set; }
    public Instant? ActivatedOn { get; set; }
    public Instant? CompletedOn { get; set; }
    public List<PokerRoundDto> Rounds { get; set; } = [];

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PokerSession, PokerSessionDetailsDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName())
            .Map(dest => dest.Facilitators, src => src.Facilitators.OrderBy(f => f.Employee!.Name.FirstName).ThenBy(f => f.Employee!.Name.LastName).Select(f => EmployeeNavigationDto.From(f.Employee!)))
            .Map(dest => dest.InvitedEmployees, src => src.InvitedEmployees.OrderBy(e => e.Employee!.Name.FirstName).ThenBy(e => e.Employee!.Name.LastName).Select(e => EmployeeNavigationDto.From(e.Employee!)))
            .Map(dest => dest.InvitedTeams, src => src.InvitedTeams.OrderBy(t => t.Team!.Name).Select(t => PlanningTeamNavigationDto.FromPlanningTeam(t.Team!)))
            .Map(dest => dest.Members, src => src.Members.OrderBy(m => m.JoinedOn))
            .Ignore(dest => dest.CanManage)
            .Map(dest => dest.Rounds, src => src.Rounds.OrderBy(r => r.Order));
    }

    /// <summary>
    /// Blanks the value of every vote in a round that has not been revealed, except the viewer's own.
    /// Participant identity is preserved so the UI can show who has voted, and the viewer's own value
    /// is kept so their selected card survives a reload.
    /// </summary>
    /// <param name="viewerUserId">The user id of the caller; <c>null</c> or a non-matching id hides every value.</param>
    public void HideUnrevealedVotes(string? viewerUserId)
    {
        Guid? viewerId = Guid.TryParse(viewerUserId, out var parsed) ? parsed : null;

        foreach (var round in Rounds)
        {
            if (_revealedStatuses.Contains(round.Status))
                continue;

            foreach (var vote in round.Votes)
            {
                if (viewerId is null || vote.Participant?.Id != viewerId)
                    vote.Value = string.Empty;
            }
        }
    }
}
