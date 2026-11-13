using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

/// <summary>
/// The record a poker round estimates. Its title and description, as they read when the round was added, are the
/// round's own label and description.
/// </summary>
public sealed record PokerRoundSubjectDto : IMapFrom<PokerRoundSubject>
{
    /// <summary>The kind of record the round estimates.</summary>
    public PokerRoundSubjectKind Kind { get; set; }

    /// <summary>The record's id.</summary>
    public Guid Id { get; set; }

    /// <summary>The key people know the record by.</summary>
    public required string DisplayKey { get; set; }
}
