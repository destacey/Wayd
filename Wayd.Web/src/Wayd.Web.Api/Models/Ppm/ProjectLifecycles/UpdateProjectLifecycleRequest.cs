using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

namespace Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

public sealed record UpdateProjectLifecycleRequest
{
    /// <summary>
    /// The name of the project lifecycle.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The description of the project lifecycle.
    /// </summary>
    public string Description { get; set; } = default!;

    public UpdateProjectLifecycleCommand ToUpdateProjectLifecycleCommand(Guid id)
    {
        return new UpdateProjectLifecycleCommand(id, Name, Description);
    }
}

public sealed class UpdateProjectLifecycleRequestValidator : AbstractValidator<UpdateProjectLifecycleRequest>
{
    public UpdateProjectLifecycleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
