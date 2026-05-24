using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.AppIntegrations;

/// <summary>
/// Classifies what shape of integration a <see cref="Connector"/> represents. The category
/// determines which background jobs and orchestrators a connection participates in — e.g. only
/// <see cref="WorkSync"/> connectors are handled by the work sync runner.
/// </summary>
public enum ConnectorCategory
{
    [Display(Name = "Unknown", Description = "The connector has not been categorized.")]
    Unknown = 0,

    [Display(Name = "Work Sync", Description = "Pulls work items, processes, workspaces, and teams from a delivery system (Azure DevOps, Jira, GitHub).")]
    WorkSync = 1,

    [Display(Name = "People Sync", Description = "Pulls employees and org structure from an HR system or directory service (Workday, Entra).")]
    PeopleSync = 2,

    [Display(Name = "AI Provider", Description = "Outbound LLM client used by Wayd features (Azure OpenAI, OpenAI, Anthropic). No data is synced.")]
    AiProvider = 3
}
