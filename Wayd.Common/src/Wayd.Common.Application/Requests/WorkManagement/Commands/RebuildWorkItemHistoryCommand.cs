using Wayd.Common.Application.Validators;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Rebuilds the effective-dated history of every work item in a workspace from the revisions already stored,
/// resolving each one afresh. Fetches nothing from the source. Returns the number of history periods written.
/// </summary>
/// <remarks>
/// The catch-up full sync a workspace's <c>NeedsFullSync</c> mark asks for runs it after reading the history, for
/// periods written before work items resolved through the status workflow engine: a replay skips revisions
/// already stored and so changes no period they wrote. Items with no stored revision keep the periods they have.
/// </remarks>
/// <param name="ConnectionId">The connection whose identity mappings name the assignees.</param>
/// <param name="WorkspaceId">The workspace whose items' history is rebuilt.</param>
public sealed record RebuildWorkItemHistoryCommand(Guid ConnectionId, Guid WorkspaceId) : ICommand<int>, ILongRunningRequest;

/// <summary>Requires the connection and the workspace.</summary>
public sealed class RebuildWorkItemHistoryCommandValidator : CustomValidator<RebuildWorkItemHistoryCommand>
{
    public RebuildWorkItemHistoryCommandValidator()
    {
        RuleFor(c => c.ConnectionId)
            .NotEmpty();

        RuleFor(c => c.WorkspaceId)
            .NotEmpty();
    }
}
