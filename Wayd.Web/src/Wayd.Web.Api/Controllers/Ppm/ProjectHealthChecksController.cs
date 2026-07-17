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
    [OpenApiOperation("Get all health checks for a project.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectHealthCheckDetailsDto>>> GetHealthChecks(Guid id, CancellationToken cancellationToken)
    {
        var healthChecks = await _dispatcher.Send(new GetProjectHealthChecksQuery(id), cancellationToken);
        return Ok(healthChecks);
    }

    [HttpGet("{id}/health-checks/{healthCheckId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get a specific health check for a project.", "")]
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
    [OpenApiOperation("Create a health check for a project.", "")]
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
    [OpenApiOperation("Update a health check for a project.", "")]
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
    [OpenApiOperation("Delete a health check from a project.", "")]
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
