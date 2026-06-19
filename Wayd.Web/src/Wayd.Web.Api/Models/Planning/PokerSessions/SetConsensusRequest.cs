using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class SetConsensusRequest
{
    public string Estimate { get; set; } = default!;

    public SetConsensusCommand ToSetConsensusCommand(Guid sessionId, Guid roundId)
    {
        return new SetConsensusCommand(sessionId, roundId, Estimate);
    }
}

public sealed class SetConsensusRequestValidator : CustomValidator<SetConsensusRequest>
{
    public SetConsensusRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Estimate).NotEmpty().MaximumLength(32);
    }
}
