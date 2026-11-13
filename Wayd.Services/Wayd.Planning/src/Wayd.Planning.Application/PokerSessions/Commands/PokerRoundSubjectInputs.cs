using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Turns the records another module asks Planning to estimate into round subjects.
/// </summary>
internal static class PokerRoundSubjectInputs
{
    /// <summary>The record as a round is added about it: its reference, title and description.</summary>
    public static PokerRoundRecord ToRecord(PokerRoundSubjectInput input) =>
        new(new PokerRoundSubject(input.Kind, input.Id, input.DisplayKey), input.Title, input.Description);
}

/// <summary>
/// Checks a record to estimate against what a round can hold. A long description is not refused: the round
/// keeps its start, since refusing would stop a session starting over one request's length.
/// </summary>
public sealed class PokerRoundSubjectInputValidator : CustomValidator<PokerRoundSubjectInput>
{
    /// <summary>The most records one command may add, which keeps a single request's work bounded.</summary>
    public const int MaxSubjectsPerRequest = 100;

    public PokerRoundSubjectInputValidator()
    {
        RuleFor(s => s.Kind).IsInEnum();
        RuleFor(s => s.Id).NotEmpty();
        RuleFor(s => s.DisplayKey).NotEmpty().MaximumLength(PokerRoundSubject.DisplayKeyMaxLength);
        RuleFor(s => s.Title).NotEmpty().MaximumLength(PokerRound.LabelMaxLength);
    }
}
