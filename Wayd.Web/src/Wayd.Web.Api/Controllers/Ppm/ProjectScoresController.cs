using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.Projects;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/projects")]
[ApiVersionNeutral]
[ApiController]
public class ProjectScoresController(ILogger<ProjectScoresController> logger, ISender sender) : ControllerBase
{
    private readonly ILogger<ProjectScoresController> _logger = logger;
    private readonly ISender _sender = sender;

    [HttpGet("{id}/scoring-context")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get the scoring context for a project (assigned model, current score, and whether the user can score).", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectScoringContextDto>> GetScoringContext(Guid id, CancellationToken cancellationToken)
    {
        var context = await _sender.Send(new GetProjectScoringContextQuery(id), cancellationToken);

        return context is not null
            ? Ok(context)
            : NotFound();
    }

    [HttpGet("{id}/scores")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get the scoring history for a project.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectScoreSummaryDto>>> GetScores(Guid id, CancellationToken cancellationToken)
    {
        var scores = await _sender.Send(new GetProjectScoresQuery(id), cancellationToken);
        return Ok(scores);
    }

    [HttpGet("{id}/scores/{scoreId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get a specific recorded score for a project.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectScoreDetailsDto>> GetScore(Guid id, Guid scoreId, CancellationToken cancellationToken)
    {
        var score = await _sender.Send(new GetProjectScoreQuery(id, scoreId), cancellationToken);

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
        var result = await _sender.Send(request.ToCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScore), new { id, scoreId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
