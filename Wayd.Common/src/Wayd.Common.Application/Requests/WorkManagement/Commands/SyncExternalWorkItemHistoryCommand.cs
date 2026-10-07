using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Validators;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Stores one batch of work item revisions and brings the affected items' effective-dated history
/// up to date, recording the watermark the batch ends at in the same save. Returns the number of
/// history periods written.
/// </summary>
/// <param name="ConnectionId">The connection whose sync produced the revisions. Scopes external identity mappings.</param>
/// <param name="WorkspaceId">The workspace whose sync read the revisions.</param>
/// <param name="Revisions">The batch's revisions, in any order.</param>
/// <param name="Watermark">The source's token for where this batch ends.</param>
/// <param name="IsLastBatch">
/// Whether the source reported this as the last batch of its stream, so the workspace's history is
/// read through to the end.
/// </param>
/// <param name="FilledWorkItemIds">
/// Set when the revisions were fetched from these items to fill their missing revision numbers:
/// they lie outside the workspace's stream, so the watermark stays where it was, and each item is
/// recorded as filled so one whose gap cannot be closed is not fetched again until it changes.
/// The source's ids, which may include items it returned no revisions for.
/// </param>
public sealed record SyncExternalWorkItemHistoryCommand(
    Guid ConnectionId,
    Guid WorkspaceId,
    IReadOnlyList<IExternalWorkItemRevision> Revisions,
    string? Watermark,
    bool IsLastBatch = false,
    IReadOnlyCollection<int>? FilledWorkItemIds = null) : ICommand<int>, ILongRunningRequest;

public sealed class SyncExternalWorkItemHistoryCommandValidator : CustomValidator<SyncExternalWorkItemHistoryCommand>
{
    public SyncExternalWorkItemHistoryCommandValidator()
    {
        RuleFor(c => c.ConnectionId)
            .NotEmpty();

        RuleFor(c => c.WorkspaceId)
            .NotEmpty();

        RuleFor(c => c.Revisions)
            .NotNull();

        RuleForEach(c => c.Revisions).ChildRules(revision =>
        {
            revision.RuleFor(r => r.WorkItemId)
                .GreaterThan(0);

            revision.RuleFor(r => r.Revision)
                .GreaterThan(0);

            revision.RuleFor(r => r.WorkType)
                .NotEmpty();

            revision.RuleFor(r => r.WorkStatus)
                .NotEmpty();
        });

        RuleFor(c => c.Watermark)
            .MaximumLength(1024);
    }
}
