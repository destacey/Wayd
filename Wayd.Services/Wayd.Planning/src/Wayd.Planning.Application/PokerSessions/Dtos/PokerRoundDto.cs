using Wayd.Common.Extensions;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

public sealed record PokerRoundDto : IMapFrom<PokerRound>
{
    public Guid Id { get; set; }
    /// <summary>
    /// What is being estimated, in a line. For a round about a record, the record's title as it read when the
    /// round was added.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The record the round estimates, or null for a round about whatever its label describes. A round about a
    /// record takes its label and description from the record, and neither can be edited.
    /// </summary>
    public PokerRoundSubjectDto? Subject { get; set; }
    /// <summary>
    /// Optional detail about what is being estimated, such as acceptance criteria or notes for the team. For a
    /// round about a record, the record's description as it read when the round was added.
    /// </summary>
    public string? Description { get; set; }
    public required string Status { get; set; }
    public string? ConsensusEstimate { get; set; }

    /// <summary>
    /// What came of last publishing the consensus to the record the round estimates, or null when it has not been
    /// published since the round was last accepted.
    /// </summary>
    public PokerRoundPublicationDto? Publication { get; set; }
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
