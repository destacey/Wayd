using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

/// <summary>
/// The record a poker round estimates, as it read when the round was added.
/// </summary>
public sealed record PokerRoundSubjectDto : IMapFrom<PokerRoundSubject>
{
    /// <summary>The kind of record the round estimates.</summary>
    public PokerRoundSubjectKind Kind { get; set; }

    /// <summary>The record's id.</summary>
    public Guid Id { get; set; }

    /// <summary>The key people know the record by.</summary>
    public required string DisplayKey { get; set; }

    /// <summary>The record's title when the round was added.</summary>
    public required string Title { get; set; }

    /// <summary>The record's description when the round was added, or null when it had none.</summary>
    public string? Description { get; set; }
}
