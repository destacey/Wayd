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
    /// Optional initial phases for the project lifecycle.
    /// </summary>
    public List<PhaseInput>? Phases { get; set; }

    public sealed record PhaseInput
    {
        /// <summary>
        /// The name of the phase.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// The description of the phase.
        /// </summary>
        public string Description { get; set; } = default!;
    }

    public CreateProjectLifecycleCommand ToCreateProjectLifecycleCommand()
    {
        var phases = Phases?.Select(p => new CreateProjectLifecycleCommand.PhaseInput(p.Name, p.Description)).ToList();
        return new CreateProjectLifecycleCommand(Name, Description, phases);
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

        RuleForEach(x => x.Phases).ChildRules(phase =>
        {
            phase.RuleFor(p => p.Name)
                .NotEmpty()
                .MaximumLength(64);

            phase.RuleFor(p => p.Description)
                .NotEmpty()
                .MaximumLength(1024);
        });
    }
}
