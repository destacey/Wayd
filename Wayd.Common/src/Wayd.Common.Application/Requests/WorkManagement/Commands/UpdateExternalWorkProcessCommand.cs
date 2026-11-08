using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Validators;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Brings a Wayd work process's name and description into line with its external process. Its work types and
/// workflows are kept in the status workflow engine by <see cref="SyncExternalWorkProcessWorkflowsCommand"/>.
/// </summary>
/// <param name="ExternalWorkProcess">The external process.</param>
public sealed record UpdateExternalWorkProcessCommand(IExternalWorkProcess ExternalWorkProcess) : ICommand;

public sealed class UpdateExternalWorkProcessCommandValidator : CustomValidator<UpdateExternalWorkProcessCommand>
{
    public UpdateExternalWorkProcessCommandValidator()
    {
        RuleFor(c => c.ExternalWorkProcess)
            .NotNull()
            .SetValidator(new IExternalWorkProcessValidator());
    }
}
