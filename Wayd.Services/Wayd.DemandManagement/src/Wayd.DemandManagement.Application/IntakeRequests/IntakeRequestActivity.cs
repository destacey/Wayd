using System.Text.Json.Nodes;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// An intake request's history as one reader may read it: only the events of the tiers they hold, and without
/// anything that names who caused an entry unless they hold the substance.
/// </summary>
/// <remarks>
/// Events carry their tier's fields — a details edit carries the description at both ends — so the history is
/// filtered by event type in the query, never redacted afterwards. A reader without the substance also gets
/// every entry without its actor: whoever submitted a request on someone's behalf is the actor of its creation,
/// and the submitter is substance.
/// <para>
/// Everything that shows a request's history reads it here — the request's Activity section and a follower's
/// changes since following — so no two can disagree about what a reader sees.
/// </para>
/// </remarks>
internal sealed class IntakeRequestActivity(IDemandManagementDbContext dbContext, IActivityLogReader activityLogReader)
{
    /// <summary>The payload fields of a status change that name the request a duplicate gives way to.</summary>
    private static readonly string[] SurvivorFields = ["fromDuplicateOfId", "toDuplicateOfId"];

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    /// <summary>
    /// One page of the history of the request <paramref name="visible"/> selects, newest first, or null when the
    /// reader cannot see it.
    /// </summary>
    /// <param name="visible">The request, already narrowed to those whose summary the reader may see.</param>
    public async Task<PagedResponse<ActivityLogDto>?> Read(
        IQueryable<IntakeRequest> visible,
        IntakeRequestReader reader,
        PageRequest paging,
        CancellationToken cancellationToken)
    {
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
            paging,
            cancellationToken);

        var visibleSurvivors = await VisibleSurvivors(activities.Items, reader, cancellationToken);
        var canSeeSubstance = tiers.Contains(IntakeRequestReadTier.Substance);

        return activities with
        {
            Items = [.. activities.Items.Select(entry => ForReader(entry, canSeeSubstance, visibleSurvivors))],
        };
    }

    /// <summary>
    /// Of the requests the page's status changes name as surviving a duplicate, those the reader may see.
    /// </summary>
    private async Task<HashSet<Guid>> VisibleSurvivors(IEnumerable<ActivityLogDto> entries, IntakeRequestReader reader, CancellationToken cancellationToken)
    {
        var named = entries
            .Where(e => e.EventType == nameof(IntakeRequestStatusChangedEvent))
            .Select(e => JsonNode.Parse(e.Payload) as JsonObject)
            .SelectMany(p => SurvivorFields.Select(field => IdIn(p, field)))
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        if (named.Length == 0)
            return [];

        return [.. await _dbContext.IntakeRequests
            .Where(r => named.Contains(r.Id))
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)];
    }

    /// <summary>The id a payload field holds, or null when it holds none.</summary>
    private static Guid? IdIn(JsonObject? payload, string field) =>
        payload?[field] is JsonValue value && value.TryGetValue<string>(out var text) && Guid.TryParse(text, out var id) ? id : null;

    /// <summary>
    /// The entry as the reader may read it. A surviving request they cannot see is left out of a status change,
    /// so its id never confirms that a hidden request exists. Without the substance, nothing names who caused the
    /// entry — the employee, the client and the actor in its payload — and a workflow switch loses its reason.
    /// </summary>
    private static ActivityLogDto ForReader(ActivityLogDto entry, bool canSeeSubstance, HashSet<Guid> visibleSurvivors)
    {
        var hidesSurvivor = entry.EventType == nameof(IntakeRequestStatusChangedEvent);
        if (canSeeSubstance && !hidesSurvivor)
            return entry;

        if (JsonNode.Parse(entry.Payload) is not JsonObject payload)
            return entry;

        if (hidesSurvivor)
        {
            foreach (var field in SurvivorFields)
            {
                if (IdIn(payload, field) is { } id && !visibleSurvivors.Contains(id))
                    payload[field] = null;
            }
        }

        if (canSeeSubstance)
            return entry with { Payload = payload.ToJsonString() };

        payload.Remove("actor");
        if (entry.EventType == nameof(StatusWorkflowSwitchedEvent))
            payload.Remove("reason");

        return entry with
        {
            Employee = null,
            ClientId = null,
            Payload = payload.ToJsonString(),
        };
    }
}
