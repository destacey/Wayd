using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Integrations.AzureDevOps.Models.Contracts;
using Wayd.Integrations.AzureDevOps.Models.Projects;
using NodaTime;

namespace Wayd.Integrations.AzureDevOps.Models.WorkItems;

internal class WorkItemResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("rev")]
    public int? Rev { get; set; }

    [JsonPropertyName("fields")]
    public required WorkItemFieldsResponse Fields { get; set; }
}

internal static class WorkItemResponseExtensions
{
    private static readonly double _defaultStackRank = 999_999_999_999D;

    public static AzdoWorkItem ToAzdoWorkItem(this WorkItemResponse workItem, IterationDto? iteration)
    {
        var created = Instant.FromDateTimeOffset(workItem.Fields.CreatedDate);
        Instant? activated = workItem.Fields.ActivatedDate.HasValue ? Instant.FromDateTimeUtc(workItem.Fields.ActivatedDate.Value) : null;
        Instant? closed = workItem.Fields.ClosedDate.HasValue ? Instant.FromDateTimeUtc(workItem.Fields.ClosedDate.Value) : null;

        var storyPoints = workItem.Fields.StoryPoints;
        if (storyPoints.HasValue && storyPoints < 0)
            storyPoints = 0;

        return new AzdoWorkItem()
        {
            Id = workItem.Id,
            Title = workItem.Fields.Title,
            WorkType = workItem.Fields.WorkItemType,
            WorkStatus = workItem.Fields.State,
            ParentId = workItem.Fields.Parent,
            AssignedTo = workItem.Fields.AssignedTo?.UniqueName,
            Created = created,
            CreatedBy = workItem.Fields.CreatedBy?.UniqueName,
            LastModified = Instant.FromDateTimeOffset(workItem.Fields.ChangedDate),
            LastModifiedBy = workItem.Fields.ChangedBy?.UniqueName,
            Priority = workItem.Fields.Priority,
            StackRank = workItem.Fields.StackRank > 0 ? workItem.Fields.StackRank : _defaultStackRank,
            ActivatedTimestamp = activated.HasValue
                ? activated < created ? created : activated
                : null,
            DoneTimestamp = closed.HasValue
                ? closed < created ? created : closed
                : null,
            TeamId = iteration?.TeamId,
            ExternalTeamIdentifier = iteration?.Identifier.ToString(),
            IterationId = iteration is not null ? workItem.Fields.IterationId : null,
            StoryPoints = storyPoints,
            Tags = string.IsNullOrWhiteSpace(workItem.Fields.Tags)
                ? []
                : [.. workItem.Fields.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        };
    }

    public static List<IExternalWorkItem> ToIExternalWorkItems(this List<WorkItemResponse> workItems, List<IterationDto> iterations, ILogger logger)
    {
        var iterationsDictionary = iterations.ToDictionary(x => x.Id, x => x);
        var result = new List<IExternalWorkItem>(workItems.Count);
        foreach (var workItem in workItems)
        {
            // System.IterationId is 0 (its default) when Azure DevOps has no iteration assigned to
            // the work item — a routine, high-volume condition for backlog items, not worth a log
            // line per occurrence. A non-zero id missing from the cache is different: it means an
            // iteration the work item genuinely references wasn't in the synced set (e.g. added
            // after the iteration cache snapshot within the same sync), which is worth surfacing.
            // Either way, sync the item with a null iteration/team rather than aborting the whole
            // workspace sync over one work item.
            if (!iterationsDictionary.TryGetValue(workItem.Fields.IterationId, out var iteration) && workItem.Fields.IterationId != 0)
            {
                logger.LogWarning("Work item {WorkItemId} references iteration {IterationId}, which was not found in the synced iteration set. Syncing without an iteration/team assignment.", workItem.Id, workItem.Fields.IterationId);
            }

            result.Add(workItem.ToAzdoWorkItem(iteration));
        }
        return result;
    }
}
