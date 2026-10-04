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
using Wayd.Work.Domain.Models;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/workspaces")]
[ApiVersionNeutral]
[ApiController]
public class WorkspacesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Workspaces)]
    [OpenApiOperation("Get a list of workspaces.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkspaceListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var workspaces = await _dispatcher.Send(new GetWorkspacesQuery(includeInactive), cancellationToken);
        return Ok(workspaces);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Workspaces)]
    [OpenApiOperation("Get workspace details.", "")]
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
    [OpenApiOperation("Set the external view work item URL template for a workspace.", "")]
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
    [OpenApiOperation("Get work items for a workspace.", "")]
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
    [OpenApiOperation("Get work item details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkItemDetailsDto>> GetWorkItem(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        // TODO: allow work item key or id
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
    [OpenApiOperation("Get a work item's project info.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkItemProjectInfoDto>> GetWorkItemProjectInfo(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        // TODO: allow work item key or id
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
    [OpenApiOperation("Update the project for a work item.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateWorkItemProject(Guid id, Guid workItemId, [FromBody] UpdateWorkItemProjectRequest request, CancellationToken cancellationToken)
    {
        if (workItemId != request.WorkItemId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateWorkItemProjectCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/work-items/{workItemKey}/children")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [OpenApiOperation("Get a work item's child work items.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetChildWorkItems(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        // TODO: allow work item key or id
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
    [OpenApiOperation("Get a work item's dependencies.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ScopedDependencyDto>>> GetWorkItemDependencies(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
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
    [OpenApiOperation(
        "Forecast when a work item will be done, by Monte Carlo simulation of its team's recent throughput, its position in the team's backlog (everything ahead of it counts; active work comes first unless `startedWorkFirst` is false), and the open predecessors it waits on.",
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
    [OpenApiOperation("Get metrics for a work item.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemProgressDailyRollupDto>>> GetMetrics(string idOrKey, string workItemKey, CancellationToken cancellationToken)
    {
        // TODO: allow work item key or id
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
    [OpenApiOperation("Search for a work item using its key or title.", "")]
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
