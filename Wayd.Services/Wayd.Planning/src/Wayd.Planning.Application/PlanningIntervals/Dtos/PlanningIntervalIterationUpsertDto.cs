using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

public sealed record PlanningIntervalIterationUpsertDto(Guid? IterationId, string Name, IterationCategory Category, LocalDateRange DateRange);

public sealed class PlanningIntervalIterationUpsertDtoValidator : CustomValidator<PlanningIntervalIterationUpsertDto>
{
    public PlanningIntervalIterationUpsertDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Category)
            .IsInEnum()
            .WithMessage(errorMessage: "A valid iteration category must be selected.");

        RuleFor(c => c.DateRange)
            .NotNull();
    }
}
