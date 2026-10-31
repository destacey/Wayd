using System.Text.Json.Serialization;

namespace Wayd.Integrations.AzureDevOps.Models.WorkItems;

/// <summary>A work item read for the project it is in now, and nothing else.</summary>
internal sealed class WorkItemProjectResponse
{
    public int Id { get; set; }

    public WorkItemProjectFieldsResponse? Fields { get; set; }
}

/// <summary>The one field of <see cref="WorkItemProjectResponse"/>.</summary>
internal sealed class WorkItemProjectFieldsResponse
{
    [JsonPropertyName("System.TeamProject")]
    public string? TeamProject { get; set; }
}
