using Wayd.Common.Application.Validators;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Records that a full sync read every work item of a managed workspace and rebuilt its history, so its later
/// syncs run as asked.
/// </summary>
/// <param name="WorkspaceId">The workspace.</param>
public sealed record CompleteWorkspaceFullSyncCommand(Guid WorkspaceId) : ICommand;

/// <summary>Requires the workspace.</summary>
public sealed class CompleteWorkspaceFullSyncCommandValidator : CustomValidator<CompleteWorkspaceFullSyncCommand>
{
    public CompleteWorkspaceFullSyncCommandValidator()
    {
        RuleFor(c => c.WorkspaceId)
            .NotEmpty();
    }
}
