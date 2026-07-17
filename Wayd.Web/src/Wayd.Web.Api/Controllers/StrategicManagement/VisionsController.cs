using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Application.Visions.Commands;
using Wayd.StrategicManagement.Application.Visions.Dtos;
using Wayd.StrategicManagement.Application.Visions.Queries;
using Wayd.StrategicManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.StrategicManagement.Visions;

namespace Wayd.Web.Api.Controllers.StrategicManagement;

[Route("api/strategic-management/[controller]")]
[ApiVersionNeutral]
[ApiController]
public class VisionsController(ILogger<VisionsController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<VisionsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Visions)]
    [OpenApiOperation("Get a list of visions.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<VisionDto>>> GetVisions([FromQuery] int? state, CancellationToken cancellationToken)
    {
        VisionState? filter = state.HasValue ? (VisionState)state.Value : null;

        var visions = await _dispatcher.Send(new GetVisionsQuery(filter), cancellationToken);

        return Ok(visions);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Visions)]
    [OpenApiOperation("Get vision details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VisionDto>> GetVision(string idOrKey, CancellationToken cancellationToken)
    {
        var vision = await _dispatcher.Send(new GetVisionQuery(idOrKey), cancellationToken);

        return vision is not null
            ? Ok(vision)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Visions)]
    [OpenApiOperation("Create a vision.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateVisionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateVisionCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVision), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Visions)]
    [OpenApiOperation("Update a vision.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateVisionRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateVisionCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Visions)]
    [OpenApiOperation("Activate a vision.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateVisionCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Visions)]
    [OpenApiOperation("Archive a vision.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveVisionCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Visions)]
    [OpenApiOperation("Delete a vision.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteVisionCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("states")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Visions)]
    [OpenApiOperation("Get a list of all vision states.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<VisionStateDto>>> GetStateOptions(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetVisionStatesQuery(), cancellationToken);
        return Ok(items.OrderBy(s => s.Order));
    }
}
