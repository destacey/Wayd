using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// An intake request's history, limited to the events of the tiers the current user may read, or null when
/// they may not see the request at all.
/// </summary>
/// <remarks>
/// Events carry their tier's fields — a details edit carries the description at both ends — so the history is
/// filtered by event type in the query, never redacted afterwards. A reader without the substance also gets
/// every entry without its actor: whoever submitted a request on someone's behalf is the actor of its creation,
/// and the submitter is substance.
/// </remarks>
public sealed record GetIntakeRequestActivitiesQuery : IQuery<PagedResponse<ActivityLogDto>?>
{
    public GetIntakeRequestActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeRequest>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<IntakeRequest, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetIntakeRequestActivitiesQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetIntakeRequestActivitiesQuery, PagedResponse<ActivityLogDto>?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<PagedResponse<ActivityLogDto>?> Handle(GetIntakeRequestActivitiesQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var visible = _dbContext.IntakeRequests
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks));

        var target = await visible
            .Select(r => new { r.Id, r.DeskId })
            .FirstOrDefaultAsync(cancellationToken);
        if (target is null)
            return null;

        List<IntakeRequestReadTier> tiers = [IntakeRequestReadTier.Summary];
        if (await visible.Where(reader.CanSeeSubstance(_dbContext.IntakeDesks)).AnyAsync(cancellationToken))
            tiers.Add(IntakeRequestReadTier.Substance);
        if (reader.IsAgentOn(target.DeskId))
            tiers.Add(IntakeRequestReadTier.Internal);

        var activities = await _activityLogReader.ReadEventTypes(
            target.Id,
            nameof(IntakeRequest),
            IntakeRequestEventTiers.EventTypesIn(tiers),
            request.Page,
            request.PageSize,
            cancellationToken);

        return tiers.Contains(IntakeRequestReadTier.Substance)
            ? activities
            : activities with { Items = [.. activities.Items.Select(WithoutActor)] };
    }

    /// <summary>
    /// The entry with nothing that names who caused it: the employee, the client, and the actor in its payload.
    /// </summary>
    private static ActivityLogDto WithoutActor(ActivityLogDto entry)
    {
        var payload = JsonNode.Parse(entry.Payload) as JsonObject;
        payload?.Remove("actor");

        return entry with
        {
            Employee = null,
            ClientId = null,
            Payload = payload?.ToJsonString() ?? entry.Payload,
        };
    }
}
