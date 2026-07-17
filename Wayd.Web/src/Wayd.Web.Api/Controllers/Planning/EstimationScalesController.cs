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
public class EstimationScalesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.EstimationScales)]
    [OpenApiOperation("Get a list of estimation scales.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EstimationScaleDto>>> GetScales(CancellationToken cancellationToken, [FromQuery] bool includeInactive = false)
    {
        var scales = await _dispatcher.Send(new GetEstimationScalesQuery(includeInactive), cancellationToken);
        return Ok(scales.OrderBy(s => s.Name));
    }

    [HttpGet("{id:int}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.EstimationScales)]
    [OpenApiOperation("Get estimation scale details.", "")]
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
    [OpenApiOperation("Create a custom estimation scale.", "")]
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
    [OpenApiOperation("Update a custom estimation scale.", "")]
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
    [OpenApiOperation("Set the active status of an estimation scale.", "")]
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
    [OpenApiOperation("Delete a custom estimation scale.", "")]
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
