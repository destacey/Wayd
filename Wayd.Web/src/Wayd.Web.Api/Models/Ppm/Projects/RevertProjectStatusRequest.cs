using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.Web.Api.Models.Ppm.Projects;

public sealed record RevertProjectStatusRequest
{
    /// <summary>
    /// The earlier status to return the Project to. Must be one of the Project's current backward status
    /// targets.
    /// </summary>
    public ProjectStatus ToStatus { get; set; }

    /// <summary>
    /// Why the Project is being reverted. Required — a reversal undoes a decision that had already been
    /// taken, and the explanation is kept in the Project's status history.
    /// </summary>
    public string Reason { get; set; } = default!;

    public RevertProjectStatusCommand ToRevertProjectStatusCommand(Guid id)
        => new RevertProjectStatusCommand(id, ToStatus, Reason);
}

public sealed class RevertProjectStatusRequestValidator : CustomValidator<RevertProjectStatusRequest>
{
    public RevertProjectStatusRequestValidator()
    {
        RuleFor(p => p.ToStatus)
            .IsInEnum();

        RuleFor(p => p.Reason)
            .NotEmpty()
                .WithMessage("A reason is required to revert a project's status.")
            .MaximumLength(1024);
    }
}
