using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.Projects;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/projects")]
[ApiVersionNeutral]
[ApiController]
public class ProjectScoresController(ILogger<ProjectScoresController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<ProjectScoresController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet("{id}/scoring-context")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation(
        "Get the scoring context for a project: the scoring model assigned to its portfolio (criteria, scales, and outputs), whether that model has been archived, and the project's current score.",
        "The scoring model is null when the project's portfolio has no model assigned, which means the project cannot be scored.")]
    [McpTool("Projects_GetScoringContext", "Get project scoring context")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectScoringContextDto>> GetScoringContext(Guid id, CancellationToken cancellationToken)
    {
        var context = await _dispatcher.Send(new GetProjectScoringContextQuery(id), cancellationToken);

        return context is not null
            ? Ok(context)
            : NotFound();
    }

    [HttpGet("{id}/scores")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation(
        "Get the scoring history for a project — every score ever recorded, each with its headline value, the model used, who scored it, and when.",
        "Returns headline values only; use Projects_GetScore for a single score's full per-criterion rating breakdown.")]
    [McpTool("Projects_GetScores", "List project scores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectScoreSummaryDto>>> GetScores(Guid id, CancellationToken cancellationToken)
    {
        var scores = await _dispatcher.Send(new GetProjectScoresQuery(id), cancellationToken);
        return Ok(scores);
    }

    [HttpGet("{id}/scores/{scoreId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation(
        "Get one recorded project score in full.",
        "Returns the frozen snapshot as it was at scoring time — every criterion rating and computed output value, plus the model name and version used. Because the snapshot is frozen, an old score reflects the model as it was then, not the model as it is now.")]
    [McpTool("Projects_GetScore", "Get project score")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectScoreDetailsDto>> GetScore(Guid id, Guid scoreId, CancellationToken cancellationToken)
    {
        var score = await _dispatcher.Send(new GetProjectScoreQuery(id, scoreId), cancellationToken);

        return score is not null
            ? Ok(score)
            : NotFound();
    }

    [HttpPost("{id}/scores")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Record a score for a project.", "")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> RecordScore(Guid id, [FromBody] RecordProjectScoreRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScore), new { id, scoreId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
