using NodaTime;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// The capacity of a team or team of teams from <paramref name="From"/> to <paramref name="To"/>, both
/// inclusive, day by day: what <see cref="GetTeamCapacityQuery"/> returns, and each person's time in each role
/// on each working day. Null when there is no <paramref name="TeamType"/> with that id.
/// </summary>
/// <param name="DaysOff">
/// Days the whole team is off for some or all of in the range beyond its working week and holidays, such as a
/// sprint's team days off. Each takes its share of everyone's time that day.
/// </param>
public sealed record GetTeamCapacityPlanQuery(Guid TeamId, TeamType TeamType, LocalDate From, LocalDate To, IReadOnlyCollection<TeamDayOff>? DaysOff = null) : IQuery<TeamCapacityPlanDto?>;

public sealed class GetTeamCapacityPlanQueryValidator : CustomValidator<GetTeamCapacityPlanQuery>
{
    /// <summary>The longest range a plan covers, in days: a year, which holds any planning interval.</summary>
    public const int MaxPlanDays = 366;

    public GetTeamCapacityPlanQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.TeamId)
            .NotEmpty();

        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The to date cannot be before the from date.")
            .Must((q, to) => Period.DaysBetween(q.From, to) < MaxPlanDays)
            .WithMessage($"The date range cannot be longer than {MaxPlanDays} days.");
    }
}

public sealed class GetTeamCapacityPlanQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IDispatcher dispatcher,
    ICurrentPrincipal currentPrincipal,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamCapacityPlanQuery, TeamCapacityPlanDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamCapacityPlanDto?> Handle(GetTeamCapacityPlanQuery request, CancellationToken cancellationToken)
    {
        var reading = await TeamCapacityReader.Read(
            _organizationDbContext,
            _dispatcher,
            _currentPrincipal,
            _schedulingSettings,
            _dateTimeProvider.Now.InUtc().Date,
            request.TeamId,
            request.TeamType,
            request.From,
            request.To,
            cancellationToken,
            request.DaysOff);

        if (reading is null)
            return null;

        var timeAwayManageable = await _currentPrincipal.EmployeesWithManageableTimeAway(
            _organizationDbContext,
            [.. reading.Here.Select(a => a.EmployeeId).Distinct()],
            cancellationToken);

        return reading.ToPlan(timeAwayManageable);
    }
}
