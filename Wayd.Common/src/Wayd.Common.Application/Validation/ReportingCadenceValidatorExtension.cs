using System.Linq.Expressions;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Application.Validation;

/// <summary>Validation rules for a reporting cadence given in a request as a length and an anchor.</summary>
public static class ReportingCadenceValidatorExtension
{
    /// <summary>
    /// Adds the rules for an optional <see cref="ReportingCadence"/> given as a length and an anchor: both or
    /// neither, a length in range, and only for a Kanban team.
    /// </summary>
    public static void RulesForReportingCadence<T>(
        this AbstractValidator<T> validator,
        Func<T, Methodology> methodology,
        Expression<Func<T, int?>> weeks,
        Expression<Func<T, LocalDate?>> anchor)
    {
        var weeksOf = weeks.Compile();

        validator.RuleFor(weeks)
            .InclusiveBetween(ReportingCadence.MinWeeks, ReportingCadence.MaxWeeks)
            .When(c => weeksOf(c) is not null)
            .WithMessage($"A cadence must be between {ReportingCadence.MinWeeks} and {ReportingCadence.MaxWeeks} weeks long.");

        validator.RuleFor(weeks)
            .Null()
            .When(c => methodology(c) != Methodology.Kanban)
            .WithMessage("Only a Kanban team has a reporting cadence; a Scrum team's sprints are its cadence.");

        validator.RuleFor(anchor)
            .NotNull()
            .When(c => weeksOf(c) is not null)
            .WithMessage("A cadence needs an anchor date.");

        validator.RuleFor(anchor)
            .Null()
            .When(c => weeksOf(c) is null)
            .WithMessage("A cadence anchor needs a cadence length.");
    }
}
