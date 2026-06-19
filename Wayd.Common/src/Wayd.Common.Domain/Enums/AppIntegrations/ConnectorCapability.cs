using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.AppIntegrations;

/// <summary>
/// A discrete integration surface a <see cref="Connector"/> can support and an admin can make a
/// connection responsible for. Capabilities are named for the resource or service the connection
/// provides, not the mechanism — each maps to one background runner / source port (e.g. only
/// connections whose connector has <see cref="WorkItems"/> are handled by the work sync runner).
/// The Display GroupName is the capability's display category — the UI groups capabilities by it
/// (multiple capabilities can share one category, e.g. a future Repos and Pipelines alongside
/// WorkItems under "Work Management").
/// </summary>
public enum ConnectorCapability
{
    [Display(Name = "Work Items", GroupName = "Work Management", Description = "Pulls work items, processes, workspaces, and teams from a delivery system (Azure DevOps, Jira, GitHub).")]
    WorkItems = 1,

    [Display(Name = "People", GroupName = "People", Description = "Pulls employees and org structure from an HR system or directory service (Workday, Entra).")]
    People = 2,

    [Display(Name = "AI Provider", GroupName = "AI Provider", Description = "Outbound LLM client used by Wayd features (Azure OpenAI). No data is synced.")]
    AiProvider = 3
}
