using Wayd.Common.Extensions;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

/// <summary>
/// A single CSV row for the planning interval import. The teams that ran the interval ride on the same
/// row as <see cref="TeamIds"/>, a semicolon-separated list of ids — the repo's multi-value column
/// convention — so one row is one planning interval and there is no second file.
/// </summary>
public sealed class ImportPlanningIntervalRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>The dates the interval ran over. Its iterations are generated inside this range.</summary>
    public DateOnly? Start { get; set; }
    public DateOnly? End { get; set; }

    /// <summary>
    /// The cadence the interval's iterations are generated from. There is no column for the iterations
    /// themselves: an interval whose real history had irregular lengths or names is corrected afterwards
    /// on its own dates screen.
    /// </summary>
    public int IterationWeeks { get; set; }

    /// <summary>
    /// Prefixes each generated iteration's name, which is otherwise just its sequence number — so
    /// iterations from different intervals stay tellable apart.
    /// </summary>
    public string? IterationPrefix { get; set; }

    /// <summary>
    /// Semicolon-separated ids of the teams that ran the interval. Blank leaves the new interval with no
    /// teams; nothing is ever replaced, since this import only creates.
    /// </summary>
    public string? TeamIds { get; set; }

    public ImportPlanningIntervalDto ToImportPlanningIntervalDto() =>
        new(
            Name,
            Description,
            // Required by the validator that runs before this mapping.
            Start!.Value.ToLocalDate(),
            End!.Value.ToLocalDate(),
            IterationWeeks,
            IterationPrefix,
            CsvList.SplitIds(TeamIds));
}

public sealed class ImportPlanningIntervalRequestValidator : CustomValidator<ImportPlanningIntervalRequest>
{
    public ImportPlanningIntervalRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .MaximumLength(2048);

        RuleFor(p => p.Start)
            .NotNull();

        RuleFor(p => p.End)
            .NotNull()
            .Must((interval, end) => interval.Start is null || interval.Start <= end)
                .WithMessage("End date must be on or after the start date.");

        RuleFor(p => p.IterationWeeks)
            .GreaterThan(0);

        // Iteration names are the prefix plus a sequence number, and an iteration name is capped at 128.
        RuleFor(p => p.IterationPrefix)
            .MaximumLength(32);

        RuleFor(p => p.TeamIds)
            .Must(CsvList.AreAllIds)
                .WithMessage("TeamIds must be a semicolon-separated list of ids.");
    }
}
