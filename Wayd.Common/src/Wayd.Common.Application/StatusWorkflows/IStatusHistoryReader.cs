using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Reads the status history of one tracked record.
/// </summary>
/// <remarks>
/// <para>
/// A service rather than a query so a module's own handler can resolve its record and then read the
/// history in one pass — a handler cannot dispatch, and the history is keyed by
/// (<c>OwnerType</c>, <c>RecordId</c>) rather than by anything a module route carries.
/// </para>
/// <para>
/// Each module supplies its own owner type, which is what keeps a request from reading the history of
/// a record it did not ask for: the route names the record, the module names the type, and neither is
/// taken from the caller.
/// </para>
/// </remarks>
public interface IStatusHistoryReader : IScopedService
{
    /// <summary>
    /// Every status change recorded for a record, newest first.
    /// </summary>
    /// <remarks>
    /// An empty list is a real answer — a record whose status has never moved has no transitions —
    /// and is not the same as the record not existing, which the caller resolves before asking.
    /// </remarks>
    Task<Result<List<StatusTransitionDto>>> Read(string ownerType, Guid recordId, CancellationToken cancellationToken);
}
