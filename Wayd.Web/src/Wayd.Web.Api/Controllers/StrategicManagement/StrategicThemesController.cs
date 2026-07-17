using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.StrategicManagement.Application.StrategicThemes.Commands;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;
using Wayd.StrategicManagement.Application.StrategicThemes.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.StrategicManagement.StrategicThemes;

namespace Wayd.Web.Api.Controllers.StrategicManagement;

[Route("api/strategic-management/strategic-themes")]
[ApiVersionNeutral]
[ApiController]
public class StrategicThemesController(ILogger<StrategicThemesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<StrategicThemesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Get a list of strategic themes.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicThemeListDto>>> GetStrategicThemes([FromQuery] int[]? state, CancellationToken cancellationToken)
    {
        StrategicThemeState[]? filter = state is { Length: > 0 }
            ? [.. state.Select(s => (StrategicThemeState)s)]
            : null;

        var themes = await _dispatcher.Send(new GetStrategicThemesQuery(filter), cancellationToken);

        return Ok(themes);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Get strategic themes details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategicThemeDetailsDto>> GetStrategicTheme(string idOrKey, CancellationToken cancellationToken)
    {
        var theme = await _dispatcher.Send(new GetStrategicThemeQuery(idOrKey), cancellationToken);

        return theme is not null
            ? Ok(theme)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Create a strategic theme.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateStrategicThemeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateStrategicThemeCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetStrategicTheme), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Update a strategic theme.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateStrategicThemeRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateStrategicThemeCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Activate a strategic theme.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateStrategicThemeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Archive a strategic theme.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveStrategicThemeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Delete a strategic theme.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteStrategicThemeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Get a list of strategic theme options.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicThemeOptionDto>>> GetStrategicThemeOptions([FromQuery] bool? includeArchived, CancellationToken cancellationToken)
    {
        var options = await _dispatcher.Send(new GetStrategicThemeOptionsQuery(includeArchived), cancellationToken);

        return Ok(options);
    }

    [HttpGet("states")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicThemes)]
    [OpenApiOperation("Get a list of all strategic theme states.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicThemeStateDto>>> GetStateOptions(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetStrategicThemeStatesQuery(), cancellationToken);
        return Ok(items.OrderBy(s => s.Order));
    }
}
