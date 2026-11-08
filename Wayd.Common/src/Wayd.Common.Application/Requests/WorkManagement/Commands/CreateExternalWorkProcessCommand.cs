using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Validators;
using Wayd.Common.Domain.Models;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Creates the Wayd work process for an external process, active, so workspaces can be set up on it. Its work
/// types and workflows are kept in the status workflow engine by <see cref="SyncExternalWorkProcessWorkflowsCommand"/>.
/// </summary>
/// <param name="ExternalWorkProcess">The external process.</param>
public sealed record CreateExternalWorkProcessCommand(IExternalWorkProcess ExternalWorkProcess) : ICommand<IntegrationState<Guid>>;

public sealed class CreateExternalWorkProcessCommandValidator : CustomValidator<CreateExternalWorkProcessCommand>
{
    public CreateExternalWorkProcessCommandValidator()
    {
        RuleFor(c => c.ExternalWorkProcess)
            .NotNull()
            .SetValidator(new IExternalWorkProcessValidator());
    }
}
