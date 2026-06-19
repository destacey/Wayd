using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

namespace Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

public sealed record AssignProjectLifecycleRequest
{
    /// <summary>
    /// The ID of the lifecycle to assign to the project.
    /// </summary>
    public Guid LifecycleId { get; set; }

    public AssignProjectLifecycleCommand ToCommand(Guid projectId)
    {
        return new AssignProjectLifecycleCommand(projectId, LifecycleId);
    }
}

public sealed class AssignProjectLifecycleRequestValidator : AbstractValidator<AssignProjectLifecycleRequest>
{
    public AssignProjectLifecycleRequestValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();
    }
}
