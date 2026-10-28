using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Dtos;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.IncidentSeveritySets;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Severity scales incidents use and which scale the organization selects.
/// </summary>
[Route("api/product-management/incident-severity-sets")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(RequirementType.All, FeatureFlags.Names.ProductManagement, FeatureFlags.Names.IncidentManagement)]
[McpTools(McpToolset.Incidents, "wayd-incidents")]
public class IncidentSeveritySetsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IncidentSeveritySets)]
    [EndpointSummary("List incident severity sets and their ordered levels.")]
    [EndpointDescription(
        "Returns every scale and which one is selected for the organization. Pass `includeInactive=true` to include deactivated custom scales. System scales cannot be edited or deleted.")]
    [McpTool("IncidentSeveritySets_GetSets", "List incident severity sets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IncidentSeveritySetDto>>> GetSets(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var sets = await _dispatcher.Send(new GetIncidentSeveritySetsQuery(includeInactive), cancellationToken);
        return Ok(sets);
    }

    [HttpGet("{id:guid}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IncidentSeveritySets)]
    [EndpointSummary("Get one incident severity set.")]
    [McpTool("IncidentSeveritySets_GetSet", "Get incident severity set")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentSeveritySetDto>> GetSet(Guid id, CancellationToken cancellationToken)
    {
        var set = await _dispatcher.Send(new GetIncidentSeveritySetQuery(id), cancellationToken);
        return set is null ? NotFound() : Ok(set);
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.IncidentSeveritySets)]
    [EndpointSummary("Create a custom incident severity set.")]
    [EndpointDescription("Requires at least two level names, ordered most severe first.")]
    [McpTool("IncidentSeveritySets_CreateSet", "Create incident severity set", Destructive = false)]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateIncidentSeveritySetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetSet), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.IncidentSeveritySets)]
    [EndpointSummary("Update a custom severity set.")]
    [EndpointDescription(
        "Replaces name, description, and the full ordered level list. Omitting an existing level id removes that level unless an incident still uses it. Refused on system sets.")]
    [McpTool("IncidentSeveritySets_UpdateSet", "Update incident severity set", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(
        Guid id,
        [FromBody] UpdateIncidentSeveritySetRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));
        }

        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}/active")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.IncidentSeveritySets)]
    [EndpointSummary("Activate or deactivate a custom severity set.")]
    [EndpointDescription(
        "Only custom sets that are not the organization default may be deactivated. The selected set cannot be deactivated.")]
    [McpTool("IncidentSeveritySets_SetActiveStatus", "Activate or deactivate incident severity set", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetActiveStatus(
        Guid id,
        [FromBody] SetIncidentSeveritySetActiveStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));
        }

        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("selection")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.IncidentSeveritySets)]
    [EndpointSummary("Select which severity set new incidents use.")]
    [EndpointDescription(
        "Changes the levels offered when opening an incident. Incidents already open keep the level they were given.")]
    [McpTool("IncidentSeveritySets_SelectSet", "Select incident severity set", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SelectSet(
        [FromBody] SelectIncidentSeveritySetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:guid}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.IncidentSeveritySets)]
    [EndpointSummary("Delete a custom severity set.")]
    [EndpointDescription("Refused for system sets, the selected set, or when an incident still uses a level in the set.")]
    [McpTool("IncidentSeveritySets_DeleteSet", "Delete incident severity set", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteIncidentSeveritySetCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
