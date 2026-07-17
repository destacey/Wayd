using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Application.Strategies.Commands;
using Wayd.StrategicManagement.Application.Strategies.Dtos;
using Wayd.StrategicManagement.Application.Strategies.Queries;
using Wayd.StrategicManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.StrategicManagement.Strategies;

namespace Wayd.Web.Api.Controllers.StrategicManagement;

[Route("api/strategic-management/[controller]")]
[ApiVersionNeutral]
[ApiController]
public class StrategiesController(ILogger<StrategiesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<StrategiesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Strategies)]
    [OpenApiOperation("Get a list of strategies.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategyListDto>>> GetStrategies([FromQuery] int? status, CancellationToken cancellationToken)
    {
        StrategyStatus? filter = status.HasValue ? (StrategyStatus)status.Value : null;

        var strategies = await _dispatcher.Send(new GetStrategiesQuery(filter), cancellationToken);

        return Ok(strategies);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Strategies)]
    [OpenApiOperation("Get strategy details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategyDetailsDto>> GetStrategy(string idOrKey, CancellationToken cancellationToken)
    {
        var strategy = await _dispatcher.Send(new GetStrategyQuery(idOrKey), cancellationToken);

        return strategy is not null
            ? Ok(strategy)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Strategies)]
    [OpenApiOperation("Create a strategy.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateStrategyRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateStrategyCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetStrategy), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Strategies)]
    [OpenApiOperation("Update a strategy.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStrategyRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateStrategyCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Strategies)]
    [OpenApiOperation("Delete a strategy.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteStrategyCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Strategies)]
    [OpenApiOperation("Get a list of all strategy statuses.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategyStatusDto>>> GetStatusOptions(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetStrategyStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(s => s.Order));
    }
}
