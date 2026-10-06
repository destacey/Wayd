using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Validators;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Applies one batch of a workspace's work item revisions to its effective-dated history, and
/// records the watermark the batch ends at in the same save.
/// </summary>
/// <param name="ConnectionId">The connection whose sync produced the revisions. Scopes external identity mappings.</param>
/// <param name="WorkspaceId">The workspace the revisions belong to.</param>
/// <param name="Revisions">The batch's revisions, in any order.</param>
/// <param name="Watermark">The source's token for where this batch ends.</param>
public sealed record SyncExternalWorkItemHistoryCommand(
    Guid ConnectionId,
    Guid WorkspaceId,
    IReadOnlyList<IExternalWorkItemRevision> Revisions,
    string? Watermark) : ICommand, ILongRunningRequest;

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
