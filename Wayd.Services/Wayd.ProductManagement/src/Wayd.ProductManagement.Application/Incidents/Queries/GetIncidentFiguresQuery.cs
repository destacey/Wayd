using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Queries;

/// <summary>The state and severity filters the incident list and its figures share.</summary>
internal static class IncidentQuery
{
    /// <summary>Incidents matching the aliases and severity levels, or every incident when a filter is empty.</summary>
    public static IQueryable<Incident> Matching(
        IQueryable<Incident> incidents,
        IReadOnlyCollection<ProductStatusAlias>? statuses,
        IReadOnlyCollection<Guid>? severityLevelIds)
    {
        if (statuses is { Count: > 0 })
        {
            var aliases = statuses.Select(status => (int)status).ToArray();
            incidents = incidents.Where(incident => aliases.Contains(incident.StatusAliasValue));
        }

        if (severityLevelIds is { Count: > 0 })
        {
            incidents = incidents.Where(incident => severityLevelIds.Contains(incident.SeverityLevelId));
        }

        return incidents;
    }
}

/// <summary>
/// The active count, the longest active incident, and the mean time to recover for the incidents
/// matching the state and severity filters.
/// </summary>
public sealed record GetIncidentFiguresQuery(
    IReadOnlyCollection<ProductStatusAlias>? Statuses = null,
    IReadOnlyCollection<Guid>? SeverityLevelIds = null) : IQuery<IncidentFiguresDto>;

public sealed class GetIncidentFiguresQueryHandler(
    IProductManagementDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetIncidentFiguresQuery, IncidentFiguresDto>
{
    private readonly IProductManagementDbContext _context = context;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<IncidentFiguresDto> Handle(
        GetIncidentFiguresQuery request,
        CancellationToken cancellationToken)
    {
        var query = IncidentQuery.Matching(
            _context.Incidents.AsNoTracking(),
            request.Statuses,
            request.SeverityLevelIds);
        var figures = IncidentDurations.Summarize(
            await MeasuredRows(query, cancellationToken),
            _dateTimeProvider.Now);

        return new IncidentFiguresDto
        {
            ActiveCount = figures.ActiveCount,
            LongestActive = figures.LongestActive is { } longest
                ? new LongestActiveIncidentDto
                {
                    Key = longest.Key,
                    Title = longest.Title,
                    ActiveMilliseconds = IncidentDurations.Milliseconds(longest.Active) ?? 0,
                }
                : null,
            MeanTimeToRecoverMilliseconds = IncidentDurations.Milliseconds(figures.MeanTimeToRecover),
        };
    }

    private static async Task<List<IncidentDurations.Row>> MeasuredRows(
        IQueryable<Incident> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Select(incident => new
            {
                incident.Key,
                incident.Title,
                incident.NoticedAt,
                incident.StartedAt,
                incident.RecoveredAt,
                incident.StatusAliasValue,
                incident.StatusCategory,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new IncidentDurations.Row(
                row.Key,
                row.Title,
                row.NoticedAt,
                row.StartedAt,
                row.RecoveredAt,
                (ProductStatusAlias)row.StatusAliasValue,
                row.StatusCategory))
            .ToList();
    }
}
