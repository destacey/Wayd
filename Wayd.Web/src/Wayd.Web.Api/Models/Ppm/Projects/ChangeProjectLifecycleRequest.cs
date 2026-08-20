using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

namespace Wayd.Web.Api.Models.Ppm.Projects;

public sealed record ChangeProjectLifecycleRequest
{
    public Guid LifecycleId { get; set; }
    public Dictionary<Guid, Guid> StageMapping { get; set; } = [];

    public ChangeProjectLifecycleCommand ToCommand(Guid projectId)
        => new(projectId, LifecycleId, StageMapping);
}

public sealed class ChangeProjectLifecycleRequestValidator : AbstractValidator<ChangeProjectLifecycleRequest>
{
    public ChangeProjectLifecycleRequestValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();

        RuleFor(x => x.StageMapping)
            .NotNull()
            .NotEmpty();
    }
}
