namespace Wayd.Web.Api.Mcp;

/// <summary>
/// A group of tools a client can ask the hosted MCP server for, so a connection loads only the tools its
/// work needs. Clients name toolsets in lower case (<c>ppm</c>, <c>planning</c>, ...); see
/// <see cref="McpToolFilter"/>.
/// </summary>
/// <remarks>
/// The names are a contract with every client configuration that selects them, so never rename one.
/// </remarks>
public enum McpToolset
{
    /// <summary>Portfolios, programs, projects, tasks, strategic initiatives and their lookups.</summary>
    Ppm,

    /// <summary>Planning intervals, roadmaps and story maps.</summary>
    Planning,

    /// <summary>The product catalog: products, product types and tag categories.</summary>
    Products,

    /// <summary>Versions, releases, release packages, deployments, environments and delivery metrics.</summary>
    Delivery,

    /// <summary>Teams, teams of teams and users.</summary>
    Teams,

    /// <summary>Work management: workspaces and their work item forecasts.</summary>
    Work,

    /// <summary>Checking, applying and following CSV imports.</summary>
    Imports,
}
