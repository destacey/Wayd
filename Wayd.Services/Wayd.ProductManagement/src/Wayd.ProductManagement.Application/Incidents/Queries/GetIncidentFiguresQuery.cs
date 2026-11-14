using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
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
        var now = _dateTimeProvider.Now;

        // IncidentActivity.IsActive, written out so SQL Server evaluates it: not closed, not
        // canceled, and not in a done or removed category.
        var active = query.Where(incident =>
            incident.StatusAliasValue != (int)ProductStatusAlias.Closed
            && incident.StatusAliasValue != (int)ProductStatusAlias.Canceled
            && incident.StatusCategory != StatusCategory.Done
            && incident.StatusCategory != StatusCategory.Removed);
        var activeCount = await active.CountAsync(cancellationToken);
        var longest = await active
            .OrderBy(incident => incident.NoticedAt)
            .ThenBy(incident => incident.Key)
            .Select(incident => new { incident.Key, incident.Title, incident.NoticedAt })
            .FirstOrDefaultAsync(cancellationToken);

        // Closed incidents that have recovered. Duration is not a numeric aggregate, so the
        // difference is the millisecond date-diff SQL Server already translates.
        var meanMilliseconds = await query
            .Where(incident =>
                incident.StatusAliasValue == (int)ProductStatusAlias.Closed
                && incident.RecoveredAt != null)
            .AverageAsync(
                incident => EF.Functions.DateDiffBigMillisecond((Instant?)incident.NoticedAt, incident.RecoveredAt),
                cancellationToken);

        return new IncidentFiguresDto
        {
            ActiveCount = activeCount,
            LongestActive = longest is null
                ? null
                : new LongestActiveIncidentDto
                {
                    Key = longest.Key,
                    Title = longest.Title,
                    ActiveMilliseconds = IncidentDurations.Milliseconds(now - longest.NoticedAt) ?? 0,
                },
            MeanTimeToRecoverMilliseconds = IncidentDurations.Milliseconds(
                meanMilliseconds is double mean ? Duration.FromMilliseconds(mean) : null),
        };
    }
}
