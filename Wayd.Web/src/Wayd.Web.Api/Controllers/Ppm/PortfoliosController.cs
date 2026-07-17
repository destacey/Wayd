using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Queries;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Scoring.Commands;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Programs.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.Portfolios;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/[controller]")]
[ApiVersionNeutral]
[ApiController]
public class PortfoliosController(ILogger<PortfoliosController> logger, IDispatcher dispatcher)
    : ControllerBase
{
    private readonly ILogger<PortfoliosController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Get a list of project portfolios.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioListDto>>> GetPortfolios([FromQuery] int[]? status, CancellationToken cancellationToken)
    {
        ProjectPortfolioStatus[]? filter = status is { Length: > 0 }
            ? [.. status.Select(s => (ProjectPortfolioStatus)s)]
            : null;

        var portfolios = await _dispatcher.Send(new GetProjectPortfoliosQuery(filter), cancellationToken);

        return Ok(portfolios);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Get project portfolio details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectPortfolioDetailsDto>> GetPortfolio(string idOrKey, CancellationToken cancellationToken)
    {
        var portfolio = await _dispatcher.Send(new GetProjectPortfolioQuery(idOrKey), cancellationToken);

        return portfolio is not null
            ? Ok(portfolio)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Create a portfolio.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreatePortfolioRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProjectPortfolioCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPortfolio), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Update a portfolio.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdatePortfolioRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateProjectPortfolioCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Activate a project portfolio.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateProjectPortfolioCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/close")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Close a project portfolio.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CloseProjectPortfolioCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Archive a project portfolio.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveProjectPortfolioCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Delete a portfolio.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProjectPortfolioCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/scoring-model")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Assign a scoring model to a portfolio, enabling project scoring.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AssignScoringModel(Guid id, [FromBody] AssignPortfolioScoringModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new AssignPortfolioScoringModelCommand(id, request.ScoringModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/scoring-model")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Clear a portfolio's scoring model, disabling new project scoring.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ClearScoringModel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ClearPortfolioScoringModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/project-ranks")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Reposition an ordered batch of projects within the portfolio's ranking.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> MoveProjectRanks(Guid id, [FromBody] MoveProjectRanksRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/project-ranks/rebalance")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Rebalance the portfolio's project ranks to clean, gap-free whole numbers.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RebalanceProjectRanks(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RebalancePortfolioRanksCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/ranking-scoreboard")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get the per-project score breakdown for the portfolio's ranking board.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioRankingScoreboardDto>> GetRankingScoreboard(Guid id, CancellationToken cancellationToken)
    {
        var scoreboard = await _dispatcher.Send(new GetPortfolioRankingScoreboardQuery(id), cancellationToken);

        return scoreboard is not null
            ? Ok(scoreboard)
            : NotFound();
    }

    [HttpGet("{idOrKey}/programs")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Programs)]
    [OpenApiOperation("Get a list of programs for the portfolio.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProgramListDto>>> GetPrograms(string idOrKey, [FromQuery] int[]? status, CancellationToken cancellationToken)
    {
        ProgramStatus[]? filter = status is { Length: > 0 }
            ? [.. status.Select(s => (ProgramStatus)s)]
            : null;

        var programs = await _dispatcher.Send(new GetProgramsQuery(PortfolioIdOrKey: idOrKey, StatusFilter: filter), cancellationToken);

        return programs is not null
            ? Ok(programs)
            : NotFound();
    }

    [HttpGet("{idOrKey}/projects")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get a list of projects for the portfolio.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectListDto>>> GetProjects(string idOrKey, [FromQuery] int[]? status, CancellationToken cancellationToken)
    {
        ProjectStatus[]? filter = status is { Length: > 0 }
            ? [.. status.Select(s => (ProjectStatus)s)]
            : null;

        var projects = await _dispatcher.Send(new GetProjectsQuery(StatusFilter: filter, PortfolioIdOrKey: idOrKey), cancellationToken);

        return projects is not null
            ? Ok(projects)
            : NotFound();
    }

    [HttpGet("{idOrKey}/strategic-initiatives")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Get a list of strategic initiatives for the portfolio.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeListDto>>> GetStrategicInitiatives(string idOrKey, [FromQuery] int[]? status, CancellationToken cancellationToken)
    {
        StrategicInitiativeStatus[]? filter = status is { Length: > 0 }
            ? [.. status.Select(s => (StrategicInitiativeStatus)s)]
            : null;

        var initiatives = await _dispatcher.Send(new GetStrategicInitiativesQuery(filter, idOrKey), cancellationToken);

        return Ok(initiatives);
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Get a list of all project portfolio statuses.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioStatusDto>>> GetPortfolioStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetProjectPortfolioStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [OpenApiOperation("Get a list of project portfolio options.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioOptionDto>>> GetPortfolioOptions(CancellationToken cancellationToken)
    {
        var options = await _dispatcher.Send(new GetProjectPortfolioOptionsQuery(), cancellationToken);

        return Ok(options);
    }
}
