using System.Text.Json.Serialization;

namespace Wayd.Integrations.AzureDevOps.Models.WorkItems;

/// <summary>A work item read for the project it is in now, and nothing else.</summary>
internal sealed class WorkItemProjectResponse
{
    /// <summary>The work item's id.</summary>
    public int Id { get; set; }

    /// <summary>The fields requested; null when Azure DevOps returns none.</summary>
    public WorkItemProjectFieldsResponse? Fields { get; set; }
}

/// <summary>The one field of <see cref="WorkItemProjectResponse"/>.</summary>
internal sealed class WorkItemProjectFieldsResponse
{
    /// <summary>The name of the project the item is in now.</summary>
    [JsonPropertyName("System.TeamProject")]
    public string? TeamProject { get; set; }
}
