using System.Text.Json.Serialization;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Integrations.AzureDevOps.Models.Contracts;
using NodaTime;

namespace Wayd.Integrations.AzureDevOps.Models.WorkItems;

/// <summary>
/// One revision from the reporting revisions API: the requested fields' full values as of that
/// revision, not what it changed.
/// </summary>
internal sealed record ReportingWorkItemRevisionResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("rev")]
    public int Rev { get; init; }

    [JsonPropertyName("fields")]
    public ReportingWorkItemRevisionFieldsResponse? Fields { get; init; }
}

/// <summary>
/// The fields the history sync requests. A field the revision does not hold is absent, which
/// for an estimate means the item had none at that revision.
/// </summary>
internal sealed record ReportingWorkItemRevisionFieldsResponse
{
    [JsonPropertyName("System.ChangedDate")]
    public DateTimeOffset ChangedDate { get; init; }

    [JsonPropertyName("System.WorkItemType")]
    public string? WorkItemType { get; init; }

    [JsonPropertyName("System.State")]
    public string? State { get; init; }

    /// <summary>The iteration's stable id; 0 when the item has no iteration.</summary>
    [JsonPropertyName("System.IterationId")]
    public int? IterationId { get; init; }

    /// <summary>An identity object only when the request sets <c>includeIdentityRef</c>; otherwise a display string.</summary>
    [JsonPropertyName("System.AssignedTo")]
    public UserResponse? AssignedTo { get; init; }

    [JsonPropertyName("Microsoft.VSTS.Scheduling.StoryPoints")]
    public double? StoryPoints { get; init; }

    [JsonPropertyName("Microsoft.VSTS.Scheduling.Effort")]
    public double? Effort { get; init; }

    [JsonPropertyName("Microsoft.VSTS.Scheduling.Size")]
    public double? Size { get; init; }
}

internal static class ReportingWorkItemRevisionResponseExtensions
{
    /// <summary>
    /// Projects the revisions onto the connector-neutral contract, dropping any without a work type
    /// or state: the history sync rejects a batch holding one, and would then fail on it every run.
    /// </summary>
    public static List<IExternalWorkItemRevision> ToIExternalWorkItemRevisions(this List<ReportingWorkItemRevisionResponse> revisions)
    {
        var result = new List<IExternalWorkItemRevision>(revisions.Count);
        foreach (var revision in revisions)
        {
            var fields = revision.Fields;
            if (fields is null || string.IsNullOrWhiteSpace(fields.WorkItemType) || string.IsNullOrWhiteSpace(fields.State))
                continue;

            result.Add(new AzdoWorkItemRevision
            {
                WorkItemId = revision.Id,
                Revision = revision.Rev,
                Changed = Instant.FromDateTimeOffset(fields.ChangedDate),
                WorkType = fields.WorkItemType,
                WorkStatus = fields.State,
                IterationId = fields.IterationId is > 0 ? fields.IterationId : null,
                AssignedTo = fields.AssignedTo?.ToUserRef(),
                StoryPoints = WorkItemResponseExtensions.ClampEstimate(fields.StoryPoints),
                Effort = WorkItemResponseExtensions.ClampEstimate(fields.Effort),
                Size = WorkItemResponseExtensions.ClampEstimate(fields.Size),
            });
        }

        return result;
    }
}
