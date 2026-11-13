using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Application.Requests.Planning.Commands;

/// <summary>
/// Starts a private poker session hosted by a record in another area, with a round about each record given. Sent
/// by the module that owns the host, which has already checked that the records belong there.
/// </summary>
/// <remarks>
/// Planning checks that the caller manages the host, through that module's <see cref="IPokerSessionHostAccess"/>,
/// and makes the caller the session's first facilitator.
/// </remarks>
/// <param name="Name">The session's name.</param>
/// <param name="HostKind">The kind of record hosting the session.</param>
/// <param name="HostId">The hosting record.</param>
/// <param name="EstimationScaleVersionId">The scale version the host estimates with, which the session votes on.</param>
/// <param name="Subjects">The records to estimate, one round each, in this order. May be empty.</param>
/// <param name="InvitedEmployeeIds">Employees invited to vote, beside the host's managers.</param>
public sealed record StartHostedPokerSessionCommand(
    string Name,
    PokerSessionHostKind HostKind,
    Guid HostId,
    Guid EstimationScaleVersionId,
    IReadOnlyList<PokerRoundSubjectInput> Subjects,
    IReadOnlyCollection<Guid>? InvitedEmployeeIds = null) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;
