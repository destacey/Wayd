using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

public sealed record PlanningIntervalIterationUpsertRequest
{
    public Guid? IterationId { get; set; }

    /// <summary>
    /// The name of the iteration.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The category of iteration.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>Gets or sets the start.</summary>
    /// <value>The start.</value>
    public LocalDate Start { get; set; }

    /// <summary>Gets or sets the end.</summary>
    /// <value>The end.</value>
    public LocalDate End { get; set; }

    public PlanningIntervalIterationUpsertDto ToPlanningIntervalIterationUpsertDto()
    {
        return new PlanningIntervalIterationUpsertDto(IterationId, Name, (IterationCategory)CategoryId, new LocalDateRange(Start, End));
    }
}

public sealed class PlanningIntervalIterationUpsertRequestValidator : CustomValidator<PlanningIntervalIterationUpsertRequest>
{
    public PlanningIntervalIterationUpsertRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(i => i.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => (IterationCategory)c.CategoryId)
            .IsInEnum()
            .WithMessage(errorMessage: "A valid iteration category must be selected.");

        RuleFor(i => i.Start)
            .NotNull();

        RuleFor(i => i.End)
            .NotNull()
            .Must((iteration, end) => iteration.Start <= end)
                .WithMessage("End date must be greater than or equal to start date");
    }
}