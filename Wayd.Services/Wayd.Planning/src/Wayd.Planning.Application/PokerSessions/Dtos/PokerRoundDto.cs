using Wayd.Common.Extensions;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

public sealed record PokerRoundDto : IMapFrom<PokerRound>
{
    public Guid Id { get; set; }
    public string? Label { get; set; }
    /// <summary>
    /// Optional detail about what is being estimated, such as acceptance criteria or notes for the team.
    /// </summary>
    public string? Description { get; set; }
    public required string Status { get; set; }
    public string? ConsensusEstimate { get; set; }
    public int Order { get; set; }
    public int VoteCount { get; set; }
    public required List<PokerVoteDto> Votes { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PokerRound, PokerRoundDto>()
            .Map(dest => dest.Status, src => src.Status.GetDisplayName())
            .Map(dest => dest.VoteCount, src => src.Votes.Count);
    }
}
