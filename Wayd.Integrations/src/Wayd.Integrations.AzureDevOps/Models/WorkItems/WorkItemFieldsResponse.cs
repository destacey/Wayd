using System.Text.Json.Serialization;

namespace Wayd.Integrations.AzureDevOps.Models.WorkItems;

internal class WorkItemFieldsResponse
{
    [JsonPropertyName("System.Title")]
    public required string Title { get; set; }

    [JsonPropertyName("System.WorkItemType")]
    public required string WorkItemType { get; set; }

    [JsonPropertyName("System.State")]
    public required string State { get; set; }

    [JsonPropertyName("System.Parent")]
    public int? Parent { get; set; }

    [JsonPropertyName("System.CreatedDate")]
    public DateTime CreatedDate { get; set; }

    [JsonPropertyName("System.CreatedBy")]
    public required UserResponse CreatedBy { get; set; }

    [JsonPropertyName("System.ChangedDate")]
    public DateTime ChangedDate { get; set; }

    [JsonPropertyName("System.ChangedBy")]
    public required UserResponse ChangedBy { get; set; }

    [JsonPropertyName("Microsoft.VSTS.Common.Priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("Microsoft.VSTS.Common.StackRank")]
    public double StackRank { get; set; }

    [JsonPropertyName("System.AssignedTo")]
    public UserResponse? AssignedTo { get; set; }

    [JsonPropertyName("System.IterationId")]
    public int IterationId { get; set; }

    [JsonPropertyName("Microsoft.VSTS.Scheduling.StoryPoints")]
    public double? StoryPoints { get; set; }

    [JsonPropertyName("Microsoft.VSTS.Scheduling.Effort")]
    public double? Effort { get; set; }

    [JsonPropertyName("Microsoft.VSTS.Scheduling.Size")]
    public double? Size { get; set; }

    [JsonPropertyName("Microsoft.VSTS.Common.ActivatedDate")]
    public DateTime? ActivatedDate { get; set; }

    [JsonPropertyName("Microsoft.VSTS.Common.ClosedDate")]
    public DateTime? ClosedDate { get; set; }

    [JsonPropertyName("System.Tags")]
    public string? Tags { get; set; }
}
