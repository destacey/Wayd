using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Application.Requests.Planning.Commands;

/// <summary>
/// Adds a round about each record given to a poker session hosted by the caller's module, such as more requests
/// to a desk's session. Sent by the module that owns the records, which has already checked that they belong in
/// the named host.
/// </summary>
/// <remarks>
/// Refused unless the session is hosted by exactly the named host, so a module's check of its records against
/// that host also holds for the session they land in, and unless the caller runs the session. Refused as a whole
/// when any record already has a round in it.
/// </remarks>
/// <param name="SessionId">The session.</param>
/// <param name="HostKind">The kind of record the session must be hosted by.</param>
/// <param name="HostId">The record the session must be hosted by.</param>
/// <param name="Subjects">The records to estimate, one round each, in this order.</param>
public sealed record AddPokerRoundSubjectsCommand(
    Guid SessionId,
    PokerSessionHostKind HostKind,
    Guid HostId,
    IReadOnlyList<PokerRoundSubjectInput> Subjects) : ICommand, IRequireLinkedEmployee;
