using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// Gets the values a new operating model for a team or team of teams starting on <paramref name="StartDate"/>
/// is pre-filled with, or null when the team does not exist.
/// </summary>
/// <remarks>
/// The zone is the one the team's parent had in effect on that date, since membership is effective-dated too,
/// falling back to the system default.
/// </remarks>
public sealed record GetOperatingModelDefaultsQuery(Guid TeamId, LocalDate StartDate) : IQuery<OperatingModelDefaultsDto?>;

public sealed class GetOperatingModelDefaultsQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings)
    : IQueryHandler<GetOperatingModelDefaultsQuery, OperatingModelDefaultsDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;

    public async Task<OperatingModelDefaultsDto?> Handle(GetOperatingModelDefaultsQuery request, CancellationToken cancellationToken)
    {
        var date = request.StartDate;

        var teamExists = await _organizationDbContext.BaseTeams.AnyAsync(t => t.Id == request.TeamId, cancellationToken);
        if (!teamExists)
            return null;

        var parent = await _organizationDbContext.BaseTeams
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.ParentMemberships)
            .Where(m => m.DateRange.Start <= date && (m.DateRange.End == null || m.DateRange.End >= date))
            .Select(m => m.Target)
            .SelectMany(p => p.OperatingModels
                .Where(om => om.DateRange.Start <= date && (om.DateRange.End == null || om.DateRange.End >= date))
                .Select(om => new { p.Name, om.TimeZone }))
            .FirstOrDefaultAsync(cancellationToken);

        var scheduling = await _schedulingSettings.Get(cancellationToken);

        return new OperatingModelDefaultsDto
        {
            TimeZone = parent?.TimeZone ?? scheduling.DefaultTimeZone,
            TimeZoneSource = parent?.Name,
            CommitmentGraceDays = scheduling.DefaultCommitmentGraceDays,
            WorkingDays = scheduling.DefaultWorkingWeek().Days,
        };
    }
}
