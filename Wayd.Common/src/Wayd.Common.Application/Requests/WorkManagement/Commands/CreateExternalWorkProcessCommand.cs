using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Validators;
using Wayd.Common.Domain.Models;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Creates the Wayd work process for an external process, with a workflow for each of its work
/// types, in one save: a failure leaves neither the process nor any workflow behind. The process's
/// work types and statuses must already exist.
/// </summary>
/// <param name="ExternalWorkProcess">The external process, its work types and their workflow states.</param>
/// <param name="WorkflowDescription">The description given to each workflow created.</param>
public sealed record CreateExternalWorkProcessCommand(IExternalWorkProcessConfiguration ExternalWorkProcess, string WorkflowDescription) : ICommand<IntegrationState<Guid>>;

public sealed class CreateExternalWorkProcessCommandValidator : CustomValidator<CreateExternalWorkProcessCommand>
{
    public CreateExternalWorkProcessCommandValidator()
    {
        RuleFor(c => c.ExternalWorkProcess)
            .NotNull()
            .SetValidator(new IExternalWorkProcessConfigurationValidator());

        RuleFor(c => c.WorkflowDescription)
            .MaximumLength(1024);
    }
}
