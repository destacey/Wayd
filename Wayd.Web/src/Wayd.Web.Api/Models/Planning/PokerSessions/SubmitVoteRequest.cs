using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Web.Api.Models.Planning.PokerSessions;

public class SubmitVoteRequest
{
    public string Value { get; set; } = default!;

    public SubmitVoteCommand ToSubmitVoteCommand(Guid sessionId, Guid roundId)
    {
        return new SubmitVoteCommand(sessionId, roundId, Value);
    }
}

public sealed class SubmitVoteRequestValidator : CustomValidator<SubmitVoteRequest>
{
    public SubmitVoteRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Value).NotEmpty().MaximumLength(32);
    }
}
