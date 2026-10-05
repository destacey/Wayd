using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.Projects;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/projects")]
[ApiVersionNeutral]
[ApiController]
public class ProjectHealthChecksController(ILogger<ProjectHealthChecksController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<ProjectHealthChecksController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet("{id}/health-checks")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get the full health check history for a project, ordered newest first.", "")]
    [McpTool("Projects_GetProjectHealthChecks", "List project health checks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectHealthCheckDetailsDto>>> GetHealthChecks(Guid id, CancellationToken cancellationToken)
    {
        var healthChecks = await _dispatcher.Send(new GetProjectHealthChecksQuery(id), cancellationToken);
        return Ok(healthChecks);
    }

    [HttpGet("{id}/health-checks/{healthCheckId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get a single project health check by ID.", "")]
    [McpTool("Projects_GetProjectHealthCheck", "Get project health check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectHealthCheckDetailsDto>> GetHealthCheck(Guid id, Guid healthCheckId, CancellationToken cancellationToken)
    {
        var healthCheck = await _dispatcher.Send(new GetProjectHealthCheckQuery(id, healthCheckId), cancellationToken);

        return healthCheck is not null
            ? Ok(healthCheck)
            : NotFound();
    }

    [HttpPost("{id}/health-checks")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation(
        "Log a new health check on a project.",
        "Creating a new check automatically expires the previously active check (only one non-expired check can exist at a time). Caller must be the project, parent portfolio, or parent program owner or manager.")]
    [McpTool("Projects_CreateProjectHealthCheck", "Log a project health check", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> CreateHealthCheck(Guid id, [FromBody] CreateProjectHealthCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CreateProjectHealthCheckCommand(id, request.Status, request.Expiration, request.Note), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetHealthCheck), new { id, healthCheckId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/health-checks/{healthCheckId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation(
        "Correct an existing health check's status, expiration, or note.",
        "This rewrites what was reported for that point in time — to report a *new* assessment, use Projects_CreateProjectHealthCheck instead, which preserves the history. Every field is overwritten from the request body, so read the check first and echo back anything that should stay the same. Caller must be the project, parent portfolio, or parent program owner or manager.")]
    [McpTool("Projects_UpdateProjectHealthCheck", "Update project health check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectHealthCheckDetailsDto>> UpdateHealthCheck(Guid id, Guid healthCheckId, [FromBody] UpdateProjectHealthCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UpdateProjectHealthCheckCommand(id, healthCheckId, request.Status, request.Expiration, request.Note), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/health-checks/{healthCheckId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation(
        "Delete a health check from a project, permanently removing it from the project's health history.",
        "Deleting the active check leaves the project with no current health status. Prefer logging a new check over deleting an old one — deletion rewrites the record of what was reported when.")]
    [McpTool("Projects_DeleteProjectHealthCheck", "Delete project health check")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteHealthCheck(Guid id, Guid healthCheckId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProjectHealthCheckCommand(id, healthCheckId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
