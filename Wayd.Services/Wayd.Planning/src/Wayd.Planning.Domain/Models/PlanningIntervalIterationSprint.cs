namespace Wayd.Planning.Domain.Models;

public sealed class PlanningIntervalIterationSprint : BaseAuditableEntity
{
    private PlanningIntervalIterationSprint() { }

    internal PlanningIntervalIterationSprint(Guid planningIntervalId, Guid planningIntervalIterationId, Guid sprintId)
    {
        PlanningIntervalId = planningIntervalId;
        PlanningIntervalIterationId = planningIntervalIterationId;
        SprintId = sprintId;
    }

    public Guid PlanningIntervalId { get; private init; }

    public Guid PlanningIntervalIterationId { get; private init; }

    public PlanningIntervalIteration PlanningIntervalIteration { get; private set; } = default!;

    public Guid SprintId { get; private init; }

    public PlanningSprint Sprint { get; private set; } = default!;
}
