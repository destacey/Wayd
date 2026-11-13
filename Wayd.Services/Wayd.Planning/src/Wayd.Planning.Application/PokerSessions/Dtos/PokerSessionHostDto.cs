using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

/// <summary>
/// The record a poker session belongs to, such as an intake desk, which lists the session and whose managers
/// facilitate it.
/// </summary>
public sealed record PokerSessionHostDto : IMapFrom<PokerSessionHost>
{
    /// <summary>The kind of record that hosts the session.</summary>
    public PokerSessionHostKind Kind { get; set; }

    /// <summary>The hosting record's id.</summary>
    public Guid Id { get; set; }
}
