using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

namespace Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

public sealed record ProjectLifecycleStageRequest
{
    /// <summary>
    /// The name of the stage.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The description of the stage.
    /// </summary>
    public string Description { get; set; } = default!;

    public AddProjectLifecycleStageCommand ToAddCommand(Guid lifecycleId)
    {
        return new AddProjectLifecycleStageCommand(lifecycleId, Name, Description);
    }

    public UpdateProjectLifecycleStageCommand ToUpdateCommand(Guid lifecycleId, Guid stageId)
    {
        return new UpdateProjectLifecycleStageCommand(lifecycleId, stageId, Name, Description);
    }
}

public sealed class ProjectLifecycleStageRequestValidator : AbstractValidator<ProjectLifecycleStageRequest>
{
    public ProjectLifecycleStageRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
