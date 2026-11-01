using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
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
        "Sorted by name. Each scale has its `id` (a UUID), `key`, `name`, `description`, `isActive` and `currentVersion`: the `number` and ordered `values` new sessions vote on, each with `isEstimate`, false for a card such as `?` that can be voted but never becomes an estimate. Only active scales are listed unless `includeInactive` is true. Requires the planning-poker feature flag; returns 404 when it is off.")]
    [McpTool("EstimationScales_GetScales", "List estimation scales")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EstimationScaleDto>>> GetScales(CancellationToken cancellationToken, [FromQuery] bool includeInactive = false)
    {
        var scales = await _dispatcher.Send(new GetEstimationScalesQuery(includeInactive), cancellationToken);
        return Ok(scales.OrderBy(s => s.Name));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.EstimationScales)]
    [EndpointSummary("Get estimation scale details.")]
    [EndpointDescription(
        "Takes the scale's UUID or key. Returns the scale, active or not, with its `currentVersion` and every published version in `versions`, newest first. A version never changes once published.")]
    [McpTool("EstimationScales_GetScale", "Get estimation scale")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstimationScaleDetailsDto>> GetScale(string idOrKey, CancellationToken cancellationToken)
    {
        var scale = await _dispatcher.Send(new GetEstimationScaleQuery(idOrKey), cancellationToken);
        return scale is not null
            ? Ok(scale)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.EstimationScales)]
    [EndpointSummary("Create a custom estimation scale.")]
    [EndpointDescription(
        "`values` are the cards in the order they are shown, each a `value` of at most 32 characters, unique on the scale, and `isEstimate`: false for a card such as `?` that can be voted but never becomes an estimate. At least 2 must be estimates. The new scale is active, with these values as version 1. Returns its `id` and `key`.")]
    [McpTool("EstimationScales_CreateScale", "Create estimation scale", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateEstimationScaleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateEstimationScaleCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScale), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.EstimationScales)]
    [EndpointSummary("Update a custom estimation scale.")]
    [EndpointDescription(
        "A whole-record overwrite: `name`, `description` and `values` are all set from the body, so an omitted `description` is cleared. Read the scale with EstimationScales_GetScale first and pass back what should stay. Values that differ from the current version's, in text, `isEstimate` or order, publish the next version; the same values publish nothing. A published version never changes, so sessions already on the scale keep voting on the version they were created on, and only sessions created afterwards use the new one. Renaming or editing the description publishes no version. Applies to any scale, including the ones Wayd seeds. Takes the scale's UUID, in the path and again as `id` in the body.")]
    [McpTool("EstimationScales_UpdateScale", "Update estimation scale")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEstimationScaleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.Id), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateEstimationScaleCommand(), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/active-status")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.EstimationScales)]
    [EndpointSummary("Set the active status of an estimation scale.")]
    [EndpointDescription(
        "`isActive` false takes the scale out of the default list, which is what new sessions are chosen from, without changing sessions that use it; true brings it back. Publishes no version. Fails when the scale already has that status, and the last active scale cannot be deactivated. Takes the scale's UUID, in the path and again as `id` in the body.")]
    [McpTool("EstimationScales_SetActiveStatus", "Activate or deactivate an estimation scale")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetActiveStatus(Guid id, [FromBody] SetEstimationScaleActiveStatusRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.Id), HttpContext));

        var result = await _dispatcher.Send(new SetEstimationScaleActiveStatusCommand(id, request.IsActive), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.EstimationScales)]
    [EndpointSummary("Delete a custom estimation scale.")]
    [EndpointDescription(
        "Permanently deletes the scale with all its versions. Refused while any poker session uses it, or when it is the last active scale; deactivate it with EstimationScales_SetActiveStatus instead. Takes the scale's UUID.")]
    [McpTool("EstimationScales_DeleteScale", "Delete estimation scale")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteEstimationScaleCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
