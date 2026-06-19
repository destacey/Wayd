using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

namespace Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

public sealed record ProjectLifecyclePhaseRequest
{
    /// <summary>
    /// The name of the phase.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The description of the phase.
    /// </summary>
    public string Description { get; set; } = default!;

    public AddProjectLifecyclePhaseCommand ToAddCommand(Guid lifecycleId)
    {
        return new AddProjectLifecyclePhaseCommand(lifecycleId, Name, Description);
    }

    public UpdateProjectLifecyclePhaseCommand ToUpdateCommand(Guid lifecycleId, Guid phaseId)
    {
        return new UpdateProjectLifecyclePhaseCommand(lifecycleId, phaseId, Name, Description);
    }
}

public sealed class ProjectLifecyclePhaseRequestValidator : AbstractValidator<ProjectLifecyclePhaseRequest>
{
    public ProjectLifecyclePhaseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
