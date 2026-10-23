using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Extensions;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Work.Workspaces;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Application.Workspaces.Commands;
using Wayd.Work.Application.Workspaces.Dtos;
using Wayd.Work.Application.Workspaces.Queries;
using Wayd.Work.Domain.Extensions;
using Wayd.Work.Domain.Models;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/workspaces")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Work, "wayd-work")]
public class WorkspacesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Workspaces)]
    [EndpointSummary("Get a list of workspaces.")]
    [EndpointDescription("Each workspace has a UUID `id`, a `key` (2 to 20 uppercase letters and digits, the prefix of its work item keys, such as `CORE` in `CORE-123`) and an `ownership`: Managed when it syncs from an external system such as Azure DevOps, Owned when Wayd holds it. Active workspaces only unless `includeInactive` is true.")]
    [McpTool("Workspaces_GetWorkspaces", "List workspaces")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkspaceListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var workspaces = await _dispatcher.Send(new GetWorkspacesQuery(includeInactive), cancellationToken);
        return Ok(workspaces);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Workspaces)]
    [EndpointSummary("Get workspace details.")]
    [EndpointDescription("`idOrKey` takes the workspace's UUID `id` or its `key` (such as `CORE`). Returns its ownership, its work process, and the `externalViewWorkItemUrlTemplate` that links its work items to the source system.")]
    [McpTool("Workspaces_GetWorkspace", "Get workspace")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkspaceDto>> Get(string idOrKey, CancellationToken cancellationToken)
    {
        GetWorkspaceQuery query;
        if (Guid.TryParse(idOrKey, out Guid guidId))
        {
            query = new GetWorkspaceQuery(guidId);
        }
        else if (idOrKey.IsValidWorkspaceKeyFormat())
        {
            query = new GetWorkspaceQuery(new WorkspaceKey(idOrKey));
        }
        else
        {
            return BadRequest(ProblemDetailsExtensions.ForUnknownIdOrKeyType(HttpContext));
        }

        var result = await _dispatcher.Send(query, cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? result.Value
                : NotFound();
    }

    [HttpPut("{id}/external-url-templates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Workspaces)]
    [EndpointSummary("Set the external view work item URL template for a workspace.")]
    [EndpointDescription("Takes the workspace's UUID `id` only. The template is the URL prefix a work item's external id is appended to, giving each work item's `externalViewWorkItemUrl` (for Azure DevOps, something like `https://dev.azure.com/org/project/_workitems/edit/`), at most 256 characters, trimmed. Null, empty or whitespace clears it. Only a Managed workspace, one synced from an external system, takes a template.")]
    [McpTool("Workspaces_SetExternalUrlTemplates", "Set external work item URL")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetExternalUrlTemplates(Guid id, [FromBody] SetExternalUrlTemplatesRequest dto, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetExternalViewWorkItemUrlTemplateCommand(id, dto.ExternalViewWorkItemUrlTemplate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #region Work Items

    [HttpGet("{idOrKey}/work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get work items for a workspace.")]
    [EndpointDescription("`idOrKey` takes the workspace's UUID `id` or its `key`. Returns every work item in the workspace, in key order, which can be thousands: to find particular items, prefer `Workspaces_SearchWorkItems`, or a team's or project's work item tools.")]
    [McpTool("Workspaces_GetWorkItems", "List workspace work items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetWorkItems(string idOrKey, CancellationToken cancellationToken)
    {
        GetWorkItemsQuery query;
        if (Guid.TryParse(idOrKey, out Guid guidId))
        {
            query = new GetWorkItemsQuery(guidId);
        }
        else if (idOrKey.IsValidWorkspaceKeyFormat())
        {
            query = new GetWorkItemsQuery(new WorkspaceKey(idOrKey));
        }
        else
        {
            return BadRequest(ProblemDetailsExtensions.ForUnknownIdOrKeyType(HttpContext));
        }

        var result = await _dispatcher.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.OrderByKey(true))
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/work-items/{workItemKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get work item details.")]
    [EndpointDescription("`idOrKey` takes the workspace's UUID `id` or its `key`, the prefix of the work item key; `workItemKey` takes the work item key, such as `CORE-123`. A work item UUID is not accepted.")]
    [McpTool("Workspaces_GetWorkItem", "Get work item")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkItemDetailsDto>> GetWorkItem(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        // TODO: allow work item key or id
        if (!workItemKey.IsValidWorkItemKeyFormat())
            return NotFound();

        var key = new WorkItemKey(workItemKey);
        GetWorkItemQuery query;
        if (Guid.TryParse(idOrKey, out Guid guidId))
        {
            query = new GetWorkItemQuery(guidId, key);
        }
        else if (idOrKey.IsValidWorkspaceKeyFormat())
        {
            query = new GetWorkItemQuery(new WorkspaceKey(idOrKey), key);
        }
        else
        {
            return BadRequest(ProblemDetailsExtensions.ForUnknownIdOrKeyType(HttpContext));
        }

        var result = await _dispatcher.Send(query, cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? result.Value
                : NotFound();
    }

    [HttpGet("{idOrKey}/work-items/{workItemKey}/project-info")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get a work item's project info.")]
    [EndpointDescription("`idOrKey` takes the workspace's UUID `id` or `key`; `workItemKey` the work item key. Returns the work item's own `project`, the `parentProject` it would otherwise inherit, and `projectSource`: WorkItem (its own assignment), Parent (inherited) or None. Read it before `Workspaces_UpdateWorkItemProject`.")]
    [McpTool("Workspaces_GetWorkItemProjectInfo", "Get work item project")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkItemProjectInfoDto>> GetWorkItemProjectInfo(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        // TODO: allow work item key or id
        if (!workItemKey.IsValidWorkItemKeyFormat())
            return NotFound();

        var key = new WorkItemKey(workItemKey);
        GetWorkItemProjectInfoQuery query;
        if (Guid.TryParse(idOrKey, out Guid guidId))
        {
            query = new GetWorkItemProjectInfoQuery(guidId, key);
        }
        else if (idOrKey.IsValidWorkspaceKeyFormat())
        {
            query = new GetWorkItemProjectInfoQuery(new WorkspaceKey(idOrKey), key);
        }
        else
        {
            return BadRequest(ProblemDetailsExtensions.ForUnknownIdOrKeyType(HttpContext));
        }

        var result = await _dispatcher.Send(query, cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? result.Value
                : NotFound();
    }

    [HttpPut("{id}/work-items/{workItemId}/update-project")]
    [MustHavePermission(ApplicationAction.ManageProjectWorkItems, ApplicationResource.Projects)]
    [EndpointSummary("Update the project for a work item.")]
    [EndpointDescription("Sets a Portfolio-tier work item's own project; other tiers are refused. `id` is the workspace's UUID, and `workItemId` the work item's UUID, which must match `workItemId` in the body; a work item key is not accepted. Returns 404 when the work item is not in that workspace. A null `projectId` clears the item's own project so it inherits its parent's again, and choosing the parent's project is the same as inheriting it. Work items beneath it that have no project of their own inherit the change.")]
    [McpTool("Workspaces_UpdateWorkItemProject", "Set a work item's project")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateWorkItemProject(Guid id, Guid workItemId, [FromBody] UpdateWorkItemProjectRequest request, CancellationToken cancellationToken)
    {
        if (workItemId != request.WorkItemId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var workItemExists = await _dispatcher.Send(new WorkItemExistsInWorkspaceQuery(id, workItemId), cancellationToken);
        if (!workItemExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToUpdateWorkItemProjectCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/work-items/{workItemKey}/children")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get a work item's child work items.")]
    [EndpointDescription("`idOrKey` takes the workspace's UUID `id` or `key`; `workItemKey` the parent's work item key. Returns its direct children only, in backlog rank order. Only Portfolio-tier work items have children.")]
    [McpTool("Workspaces_GetChildWorkItems", "List child work items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetChildWorkItems(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        // TODO: allow work item key or id
        if (!workItemKey.IsValidWorkItemKeyFormat())
            return NotFound();

        var key = new WorkItemKey(workItemKey);
        GetChildWorkItemsQuery query;
        if (Guid.TryParse(idOrKey, out Guid guidId))
        {
            query = new GetChildWorkItemsQuery(guidId, key);
        }
        else if (idOrKey.IsValidWorkspaceKeyFormat())
        {
            query = new GetChildWorkItemsQuery(new WorkspaceKey(idOrKey), key);
        }
        else
        {
            return BadRequest(ProblemDetailsExtensions.ForUnknownIdOrKeyType(HttpContext));
        }

        var result = await _dispatcher.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.OrderBy(w => w.StackRank))
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/work-items/{workItemKey}/dependencies")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get a work item's dependencies.")]
    [EndpointDescription("`idOrKey` takes the workspace's UUID `id` or `key`; `workItemKey` the work item key. Returns its current dependency links, oldest first, each naming the other work item (`dependency`) and its `type` from this item's side: Predecessor when this item waits on it, Successor when it waits on this item. `state`, `health` and `scope` describe the link.")]
    [McpTool("Workspaces_GetWorkItemDependencies", "Get work item dependencies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ScopedDependencyDto>>> GetWorkItemDependencies(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        if (!workItemKey.IsValidWorkItemKeyFormat())
            return NotFound();

        var key = new WorkItemKey(workItemKey);

        var result = await _dispatcher.Send(new GetWorkItemDependenciesQuery(idOrKey, key), cancellationToken);


        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value.OrderBy(w => w.CreatedOn))
                : NotFound();
    }

    [HttpGet("{idOrKey}/work-items/{workItemKey}/forecast")]
    [FeatureGate(FeatureFlags.Names.DeliveryForecasting)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary(
        "Forecast when a work item will be done, by Monte Carlo simulation of its team's recent throughput, its position in the team's backlog (everything ahead of it counts; active work comes first unless `startedWorkFirst` is false), and the open predecessors it waits on.")]
    [EndpointDescription(
        "A portfolio work item (an Epic or Feature, say) is forecast from its open backlog descendants. Returns an `outcome` (Forecast, Done, Not Enough History, Blocked by Dependency, Cannot Forecast, Nothing Remaining), `backlogPosition`, and on Forecast completion `percentiles` (a `date` per `confidence`), plus `chanceOfFinishingByTargetDate` (0 to 1) when `targetDate` is given. `issues` explain what could not be forecast (No Team, Not a Backlog Item, Not Enough History); `dependencies` gives each predecessor's `shareOfTrialsSettingFinish`. Requires the delivery-forecasting feature flag; returns 404 when it is off.")]
    [McpTool("Workspaces_GetWorkItemForecast", "Forecast work item completion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkItemForecastDto>> GetWorkItemForecast(
        string idOrKey,
        string workItemKey,
        [FromQuery] string? targetDate,
        [FromQuery] int? lookbackDays,
        [FromQuery] bool? ignoreDependencies,
        [FromQuery] bool? startedWorkFirst,
        CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(targetDate, out var parsedTargetDate))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (!workItemKey.IsValidWorkItemKeyFormat())
            return NotFound();

        var key = new WorkItemKey(workItemKey);
        var options = new ForecastOptions
        {
            LookbackDays = lookbackDays ?? ForecastOptions.DefaultLookbackDays,
            IgnoreDependencies = ignoreDependencies ?? false,
            StartedWorkFirst = startedWorkFirst ?? true,
        };

        var forecast = await _dispatcher.Send(new GetWorkItemForecastQuery(idOrKey, key, parsedTargetDate, options), cancellationToken);

        return forecast is not null
            ? Ok(forecast)
            : NotFound();
    }

    [HttpGet("{idOrKey}/work-items/{workItemKey}/metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get metrics for a work item.")]
    [EndpointDescription("A daily cumulative flow of the work item's Requirement-tier work, itself or, for a Portfolio-tier item, its descendants: for each `date` from the earliest creation to the day it was done (or today), how many items were Proposed, Active and Done. Removed work is left out. `idOrKey` takes the workspace's UUID `id` or `key`; `workItemKey` the work item key.")]
    [McpTool("Workspaces_GetWorkItemMetrics", "Get work item progress metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemProgressDailyRollupDto>>> GetMetrics(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        // TODO: allow work item key or id
        if (!workItemKey.IsValidWorkItemKeyFormat())
            return NotFound();

        var key = new WorkItemKey(workItemKey);
        GetWorkItemMetricsQuery query;
        if (Guid.TryParse(idOrKey, out Guid guidId))
        {
            query = new GetWorkItemMetricsQuery(guidId, key);
        }
        else if (idOrKey.IsValidWorkspaceKeyFormat())
        {
            query = new GetWorkItemMetricsQuery(new WorkspaceKey(idOrKey), key);
        }
        else
        {
            return BadRequest(ProblemDetailsExtensions.ForUnknownIdOrKeyType(HttpContext));
        }

        var result = await _dispatcher.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("work-items/search")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Search for a work item using its key or title.")]
    [EndpointDescription("Searches every workspace: `query` matches part of a work item's key, its parent's key, or its title (a word prefix when full-text search is available). Returns at most `top` work items (default 50) in key order. Each result's `workspace` key and `key` are what the other work item tools take.")]
    [McpTool("Workspaces_SearchWorkItems", "Search work items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> SearchWorkItems(string query, CancellationToken cancellationToken, int top = 50)
    {
        var result = await _dispatcher.Send(new SearchWorkItemsQuery(query, top), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.OrderByKey(true))
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Work Items
}
