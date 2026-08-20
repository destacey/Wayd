using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

namespace Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

public sealed record CreateProjectLifecycleRequest
{
    /// <summary>
    /// The name of the project lifecycle.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The description of the project lifecycle.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// Optional initial stages for the project lifecycle.
    /// </summary>
    public List<StageInput>? Stages { get; set; }

    public sealed record StageInput
    {
        /// <summary>
        /// The name of the stage.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// The description of the stage.
        /// </summary>
        public string Description { get; set; } = default!;
    }

    public CreateProjectLifecycleCommand ToCreateProjectLifecycleCommand()
    {
        var stages = Stages?.Select(p => new CreateProjectLifecycleCommand.StageInput(p.Name, p.Description)).ToList();
        return new CreateProjectLifecycleCommand(Name, Description, stages);
    }
}

public sealed class CreateProjectLifecycleRequestValidator : AbstractValidator<CreateProjectLifecycleRequest>
{
    public CreateProjectLifecycleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleForEach(x => x.Stages).ChildRules(stage =>
        {
            stage.RuleFor(p => p.Name)
                .NotEmpty()
                .MaximumLength(64);

            stage.RuleFor(p => p.Description)
                .NotEmpty()
                .MaximumLength(1024);
        });
    }
}
