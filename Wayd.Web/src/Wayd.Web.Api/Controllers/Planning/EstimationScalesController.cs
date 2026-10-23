using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Planning.Application.EstimationScales.Commands;
using Wayd.Planning.Application.EstimationScales.Dtos;
using Wayd.Planning.Application.EstimationScales.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.EstimationScales;

namespace Wayd.Web.Api.Controllers.Planning;

[Route("api/planning/estimation-scales")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.PlanningPoker)]
[McpTools(McpToolset.Planning, "wayd-estimation")]
public class EstimationScalesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.EstimationScales)]
    [EndpointSummary("Get a list of estimation scales.")]
    [EndpointDescription(
        "Sorted by name. Each scale has an integer `id`, `name`, `description`, `isActive` and its ordered `values`. Only active scales are listed unless `includeInactive` is true. Requires the planning-poker feature flag; returns 404 when it is off.")]
    [McpTool("EstimationScales_GetScales", "List estimation scales")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EstimationScaleDto>>> GetScales(CancellationToken cancellationToken, [FromQuery] bool includeInactive = false)
    {
        var scales = await _dispatcher.Send(new GetEstimationScalesQuery(includeInactive), cancellationToken);
        return Ok(scales.OrderBy(s => s.Name));
    }

    [HttpGet("{id:int}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.EstimationScales)]
    [EndpointSummary("Get estimation scale details.")]
    [EndpointDescription(
        "Takes the scale's integer id. Returns the scale, active or not, with its ordered `values`.")]
    [McpTool("EstimationScales_GetScale", "Get estimation scale")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstimationScaleDto>> GetScale(int id, CancellationToken cancellationToken)
    {
        var scale = await _dispatcher.Send(new GetEstimationScaleQuery(id), cancellationToken);
        return scale is not null
            ? Ok(scale)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.EstimationScales)]
    [EndpointSummary("Create a custom estimation scale.")]
    [EndpointDescription(
        "`values` are the cards in the order they are shown: at least 2, each at most 32 characters. The new scale is active. Returns its integer id.")]
    [McpTool("EstimationScales_CreateScale", "Create estimation scale", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Int))]
    public async Task<ActionResult<int>> Create([FromBody] CreateEstimationScaleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateEstimationScaleCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScale), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:int}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.EstimationScales)]
    [EndpointSummary("Update a custom estimation scale.")]
    [EndpointDescription(
        "A whole-record overwrite: `name`, `description` and `values` are all set from the body, so an omitted `description` is cleared and `values` replaces the whole list. Read the scale with EstimationScales_GetScale first and pass back what should stay. Applies to any scale, including the ones Wayd seeds, and every session on the scale shows the new values, completed ones included. Takes the scale's integer id, in the path and again as `estimationScaleId` in the body.")]
    [McpTool("EstimationScales_UpdateScale", "Update estimation scale")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateEstimationScaleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.EstimationScaleId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.EstimationScaleId), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateEstimationScaleCommand(), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:int}/active-status")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.EstimationScales)]
    [EndpointSummary("Set the active status of an estimation scale.")]
    [EndpointDescription(
        "`isActive` false takes the scale out of the default list, which is what new sessions are chosen from, without changing sessions that use it; true brings it back. Fails when the scale already has that status, and the last active scale cannot be deactivated. Takes the scale's integer id, in the path and again as `id` in the body.")]
    [McpTool("EstimationScales_SetActiveStatus", "Activate or deactivate an estimation scale")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetActiveStatus(int id, [FromBody] SetEstimationScaleActiveStatusRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.Id), HttpContext));

        var result = await _dispatcher.Send(new SetEstimationScaleActiveStatusCommand(id, request.IsActive), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:int}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.EstimationScales)]
    [EndpointSummary("Delete a custom estimation scale.")]
    [EndpointDescription(
        "Permanently deletes the scale. Refused while any poker session uses it, or when it is the last active scale; deactivate it with EstimationScales_SetActiveStatus instead. Takes the scale's integer id.")]
    [McpTool("EstimationScales_DeleteScale", "Delete estimation scale")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteEstimationScaleCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
