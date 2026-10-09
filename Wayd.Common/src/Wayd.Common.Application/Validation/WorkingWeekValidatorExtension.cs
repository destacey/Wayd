using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Application.Validation;

public static class WorkingWeekValidatorExtension
{
    /// <summary>
    /// Requires days that make a <see cref="WorkingWeek"/>: at least one, each a day of the week.
    /// </summary>
    public static IRuleBuilderOptions<T, TDays> IsWorkingWeek<T, TDays>(this IRuleBuilder<T, TDays> ruleBuilder)
        where TDays : IEnumerable<IsoDayOfWeek>? =>
        ruleBuilder
            .Must(days => WorkingWeek.Create(days).IsSuccess)
            .WithMessage("Choose at least one working day of the week.");
}
