namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

/// <summary>
/// One planning interval on its way in, with the teams that ran it.
/// </summary>
/// <remarks>
/// Iterations are not carried: they are generated from <see cref="IterationWeeks"/> and
/// <see cref="IterationPrefix"/> exactly as a hand-created interval's are, with the trailing one marked
/// Innovation and Planning. An interval whose real history had irregular iteration lengths or names is
/// corrected afterwards on its own dates screen, which is the only place iterations are editable.
/// <para>
/// <see cref="TeamIds"/> is the roster the interval starts with. This import only creates, so the list
/// never replaces an existing one: blank means an interval with no teams, not a cleared roster.
/// </para>
/// </remarks>
public sealed record ImportPlanningIntervalDto(
    string Name,
    string? Description,
    LocalDate Start,
    LocalDate End,
    int IterationWeeks,
    string? IterationPrefix,
    IReadOnlyList<Guid> TeamIds);

public sealed class ImportPlanningIntervalDtoValidator : CustomValidator<ImportPlanningIntervalDto>
{
    public ImportPlanningIntervalDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .MaximumLength(2048);

        RuleFor(p => p.End)
            .GreaterThanOrEqualTo(p => p.Start)
            .WithMessage("End date must be on or after the start date.");

        RuleFor(p => p.IterationWeeks)
            .GreaterThan(0);

        // Iteration names are the prefix plus a sequence number, and an iteration name is capped at 128.
        RuleFor(p => p.IterationPrefix)
            .MaximumLength(32);
    }
}
