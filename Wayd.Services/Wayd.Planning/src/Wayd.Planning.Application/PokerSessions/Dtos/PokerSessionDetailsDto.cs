using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Extensions;
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
    public UserNavigationDto? Facilitator { get; set; }
    public EstimationScaleDto? EstimationScale { get; set; }
    public Instant? ActivatedOn { get; set; }
    public Instant? CompletedOn { get; set; }
    public List<PokerRoundDto> Rounds { get; set; } = [];

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PokerSession, PokerSessionDetailsDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName())
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
