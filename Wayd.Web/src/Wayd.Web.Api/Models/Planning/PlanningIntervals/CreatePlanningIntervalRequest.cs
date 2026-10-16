using Wayd.Planning.Application.PlanningIntervals.Commands;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

public sealed record CreatePlanningIntervalRequest
{
    /// <summary>Gets the team name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets the team description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the start.</summary>
    public LocalDate Start { get; set; }

    /// <summary>Gets or sets the end.</summary>
    public LocalDate End { get; set; }

    /// <summary>
    /// Gets or sets the length of iterations in weeks.
    /// </summary>
    public int IterationWeeks { get; set; }

    /// <summary>
    /// Gets or sets the iteration prefix.
    /// </summary>
    public string? IterationPrefix { get; set; }

    public CreatePlanningIntervalCommand ToCreatePlanningIntervalCommand()
    {
        return new CreatePlanningIntervalCommand(Name, Description, new LocalDateRange(Start, End), IterationWeeks, IterationPrefix);
    }
}

public sealed class CreatePlanningIntervalRequestValidator : CustomValidator<CreatePlanningIntervalRequest>
{
    public CreatePlanningIntervalRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Description)
            .MaximumLength(2048);

        RuleFor(t => t.Start)
            .NotNull();

        RuleFor(t => t.End)
            .NotNull()
            .Must((interval, end) => interval.Start <= end)
                .WithMessage("End date must be greater than or equal to start date");

        RuleFor(t => t.IterationWeeks)
            .GreaterThan(0);
    }
}
