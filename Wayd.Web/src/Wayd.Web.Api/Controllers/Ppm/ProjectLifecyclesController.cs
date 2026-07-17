using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/project-lifecycles")]
[ApiVersionNeutral]
[ApiController]
public class ProjectLifecyclesController(ILogger<ProjectLifecyclesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<ProjectLifecyclesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Get a list of project lifecycles.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectLifecycleListDto>>> GetProjectLifecycles([FromQuery] ProjectLifecycleState? state, CancellationToken cancellationToken)
    {
        var lifecycles = await _dispatcher.Send(new GetProjectLifecyclesQuery(state), cancellationToken);

        return Ok(lifecycles);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Get project lifecycle details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectLifecycleDetailsDto>> GetProjectLifecycle(string idOrKey, CancellationToken cancellationToken)
    {
        var lifecycle = await _dispatcher.Send(new GetProjectLifecycleQuery(idOrKey), cancellationToken);

        return lifecycle is not null
            ? Ok(lifecycle)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Create a project lifecycle.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProjectLifecycleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProjectLifecycleCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProjectLifecycle), new { idOrKey = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Update a project lifecycle.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateProjectLifecycleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateProjectLifecycleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Delete a project lifecycle.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProjectLifecycleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Activate a project lifecycle.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateProjectLifecycleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Archive a project lifecycle.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveProjectLifecycleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/phases")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Add a phase to a project lifecycle.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> AddPhase(Guid id, [FromBody] ProjectLifecyclePhaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProjectLifecycle), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/phases/{phaseId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Update a phase in a project lifecycle.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdatePhase(Guid id, Guid phaseId, [FromBody] ProjectLifecyclePhaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateCommand(id, phaseId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/phases/{phaseId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Remove a phase from a project lifecycle.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemovePhase(Guid id, Guid phaseId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveProjectLifecyclePhaseCommand(id, phaseId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/phases/reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [OpenApiOperation("Reorder phases in a project lifecycle.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderPhases(Guid id, [FromBody] ReorderProjectLifecyclePhasesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderProjectLifecyclePhasesCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
