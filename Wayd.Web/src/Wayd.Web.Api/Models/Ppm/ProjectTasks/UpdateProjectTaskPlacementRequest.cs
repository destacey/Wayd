using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

namespace Wayd.Web.Api.Models.Ppm.ProjectTasks;

public sealed record UpdateProjectTaskPlacementRequest
{
    /// <summary>
    /// The ID of the task.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// The ID of the parent phase or task. If it matches a phase, the task becomes a root task in that phase.
    /// If it matches a task, the task becomes a child of that task.
    /// </summary>
    public Guid ParentId { get; set; }

    /// <summary>
    /// The new order/position of the task within its parent.
    /// </summary>
    public int? Order { get; set; }

    public UpdateProjectTaskPlacementCommand ToUpdateProjectTaskPlacementCommand(Guid projectId)
        => new(projectId, TaskId, ParentId, Order);
}

public sealed class UpdateProjectTaskPlacementRequestValidator : CustomValidator<UpdateProjectTaskPlacementRequest>
{
    public UpdateProjectTaskPlacementRequestValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty();

        RuleFor(x => x.ParentId)
            .NotEmpty()
            .WithMessage("ParentId is required and cannot be an empty GUID.");

        RuleFor(x => x.Order)
            .GreaterThan(0)
                .When(x => x.Order.HasValue)
                .WithMessage("Order must be greater than 0 when specified.");
    }
}
