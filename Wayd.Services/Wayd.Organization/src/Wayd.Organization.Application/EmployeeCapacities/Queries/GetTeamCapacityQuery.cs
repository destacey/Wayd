using NodaTime;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// The capacity of a team or team of teams from <paramref name="From"/> to <paramref name="To"/>, both
/// inclusive: who is allocated to it, and their person-days over its working days after the holidays each
/// person takes and their time away. Null when there is no <paramref name="TeamType"/> with that id.
/// </summary>
/// <remarks>
/// A team of teams counts only the people allocated to it directly. <see cref="GetTeamOfTeamsCapacityRollupQuery"/>
/// adds the teams beneath it.
/// </remarks>
public sealed record GetTeamCapacityQuery(Guid TeamId, TeamType TeamType, LocalDate From, LocalDate To) : IQuery<TeamCapacityDto?>;

public sealed class GetTeamCapacityQueryValidator : CustomValidator<GetTeamCapacityQuery>
{
    /// <summary>The longest range a capacity read covers, in days: two years.</summary>
    public const int MaxDays = 731;

    public GetTeamCapacityQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.TeamId)
            .NotEmpty();

        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The to date cannot be before the from date.")
            .Must((q, to) => Period.DaysBetween(q.From, to) < MaxDays)
            .WithMessage($"The date range cannot be longer than {MaxDays} days.");
    }
}

public sealed class GetTeamCapacityQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IDispatcher dispatcher,
    ICurrentPrincipal currentPrincipal,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamCapacityQuery, TeamCapacityDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamCapacityDto?> Handle(GetTeamCapacityQuery request, CancellationToken cancellationToken)
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
            cancellationToken);

        return reading?.ToCapacity();
    }
}
