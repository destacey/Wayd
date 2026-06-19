namespace Wayd.Web.Api.Models.Ppm.ProjectTasks;

public sealed record AddTaskDependencyRequest
{
    /// <summary>
    /// The ID of the predecessor task (the task that must complete first).
    /// </summary>
    public Guid PredecessorId { get; set; }

    /// <summary>
    /// The ID of the successor task (the task that depends on the predecessor).
    /// </summary>
    public Guid SuccessorId { get; set; }
}

public sealed class AddTaskDependencyRequestValidator : CustomValidator<AddTaskDependencyRequest>
{
    public AddTaskDependencyRequestValidator()
    {
        RuleFor(x => x.PredecessorId)
            .NotEmpty()
            .WithMessage("PredecessorId is required.");

        RuleFor(x => x.SuccessorId)
            .NotEmpty()
            .WithMessage("SuccessorId is required.");

        RuleFor(x => x)
            .Must(x => x.PredecessorId != x.SuccessorId)
            .WithMessage("A task cannot depend on itself.");
    }
}
