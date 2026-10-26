using CsvHelper;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Commands;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Imports;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Imports;
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
using Wayd.Web.Api.Models.Ppm.Finalization;
using Wayd.Web.Api.Models.Ppm.Portfolios;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/[controller]")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Ppm, "wayd-ppm")]
public class PortfoliosController(ILogger<PortfoliosController> logger, IDispatcher dispatcher, ICsvService csvService)
    : ControllerBase
{
    private readonly ILogger<PortfoliosController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Get a list of project portfolios.")]
    [McpTool("Portfolios_GetPortfolios", "List portfolios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioListDto>>> GetPortfolios([FromQuery] ProjectPortfolioStatus[]? status, CancellationToken cancellationToken)
    {
        var portfolios = await _dispatcher.Send(new GetProjectPortfoliosQuery(status), cancellationToken);

        return Ok(portfolios);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Get project portfolio details.")]
    [McpTool("Portfolios_GetPortfolio", "Get portfolio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectPortfolioDetailsDto>> GetPortfolio(string idOrKey, CancellationToken cancellationToken)
    {
        var portfolio = await _dispatcher.Send(new GetProjectPortfolioQuery(idOrKey), cancellationToken);

        return portfolio is not null
            ? Ok(portfolio)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary(
        "Get a portfolio's activity history, newest first: every change recorded on the portfolio itself — details, roles, scoring model, status.")]
    [EndpointDescription(
        "Its programs and projects keep their own histories. Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("Portfolios_GetActivities", "Get portfolio activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetPortfolioActivitiesQuery(new IdOrKey(idOrKey), new PageRequest(page, pageSize)), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Create a portfolio.")]
    [EndpointDescription(
        "It starts in Proposed status — use Portfolios_Activate to make it active, which also stamps its start date. Role lists REPLACE the existing assignments for that role — they do not add to them. An omitted or empty list REMOVES everyone currently holding that role. Always read the current record first and pass back the full membership you intend to keep, including people you are not changing. Changes a record other people rely on, so confirm with the user before calling.")]
    [McpTool("Portfolios_Create", "Create portfolio", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreatePortfolioRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProjectPortfolioCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPortfolio), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Submit a csv file of portfolios to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ProjectPortfolioImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportPortfolioRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedPortfolios = _csvService.ReadCsv<ImportPortfolioRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportProjectPortfolioDto>> rows = [];
            var validator = new ImportPortfolioRequestValidator();
            foreach (var portfolio in importedPortfolios)
            {
                var validationResults = await validator.ValidateAsync(portfolio, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Name: {portfolio.Name})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportProjectPortfolioDto>(portfolio.ImportId, portfolio.ToImportProjectPortfolioDto()));
            }

            var result = await _dispatcher.Send(new ImportProjectPortfoliosCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    /// <summary>
    /// Closes out imported programs and portfolios — the last step of a PPM import. The domain only lets
    /// work be added to an active program or portfolio, but only lets one be closed once everything inside
    /// it is already closed, so historical items are imported active and finished here.
    /// </summary>
    [HttpPost("finalize/import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Submit a csv file of PPM finalizations to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [EndpointDescription("Completes or cancels programs and closes or archives portfolios, after their contents have been imported.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(PpmFinalizationImportDefinition.ImportKey)]
    public async Task<ActionResult> FinalizeImport([FromForm, CsvRows(typeof(ImportPpmFinalizationRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedItems = _csvService.ReadCsv<ImportPpmFinalizationRequest>(file.OpenReadStream());

            List<SubmittedImportRow<FinalizePpmItemDto>> rows = [];
            var validator = new ImportPpmFinalizationRequestValidator();
            foreach (var item in importedItems)
            {
                var validationResults = await validator.ValidateAsync(item, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Id: {item.Id})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<FinalizePpmItemDto>(item.ImportId, item.ToFinalizePpmItemDto()));
            }

            var result = await _dispatcher.Send(new ImportPpmFinalizationsCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Update a portfolio's name, description, and role assignments.")]
    [EndpointDescription(
        "This is a whole-record update, not a patch: every field is overwritten from the request body, so omitting a field clears it. For a partial change, use Portfolios_Patch instead. Read the record first and echo back every value that should stay the same. Role lists REPLACE the existing assignments for that role — they do not add to them. An omitted or empty list REMOVES everyone currently holding that role. Always read the current record first and pass back the full membership you intend to keep, including people you are not changing. The id in the body must match the id path parameter. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a record other people rely on, so confirm with the user before calling.")]
    [McpTool("Portfolios_Update", "Update portfolio", Idempotent = false)]
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

    [HttpPatch("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Change some of a portfolio's details or roles with a JSON Patch (RFC 6902), leaving the rest as they are.")]
    [EndpointDescription(
        "Prefer this to Portfolios_Update for any partial change. Paths are the update's camelCase fields: /name, /description, /sponsorIds, /ownerIds, /managerIds. `replace` changes a field; `add` with `/managerIds/-` appends one id to a list; to remove from a list, `replace` the whole list with the set that should remain — never remove by index. A field the patch does not name keeps its value. Role ids are employee ids. Requires delivery leadership — the caller must be an Owner or Manager of the record; a permission claim alone is not enough. Changes a record other people rely on, so confirm with the user before calling.")]
    [McpTool("Portfolios_Patch", "Patch portfolio")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult> PatchPortfolio(Guid id, [FromBody] JsonPatchDocument<UpdatePortfolioRequest> patchDocument, CancellationToken cancellationToken)
        => this.Patch(
            patchDocument,
            ct => _dispatcher.Send(new GetProjectPortfolioQuery(id), ct),
            $"Portfolio with ID '{id}' not found.",
            UpdatePortfolioRequest.FromDto,
            (request, ct) => _dispatcher.Send((request with { Id = id }).ToUpdateProjectPortfolioCommand(), ct),
            cancellationToken);

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Activate a proposed portfolio.")]
    [EndpointDescription(
        "**This also sets the portfolio's start date to today, and the date cannot be backdated or changed by this call** — do not use it to fix up a portfolio that actually started earlier. Only proposed portfolios can be activated. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Portfolios_Activate", "Activate portfolio")]
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
    [EndpointSummary("Close an active or on-hold portfolio.")]
    [EndpointDescription(
        "**This also sets the portfolio's end date to today, and the date cannot be backdated by this call.** Only active or on-hold portfolios can be closed. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Portfolios_Close", "Close portfolio")]
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
    [EndpointSummary("Archive a closed portfolio, removing it from active use.")]
    [EndpointDescription(
        "Only closed portfolios can be archived — close it first. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Portfolios_Archive", "Archive portfolio")]
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
    [EndpointSummary("Delete a portfolio.")]
    [EndpointDescription(
        "Permanent, and only for a portfolio still in Proposed status — any other status is rejected; close or archive it instead (Portfolios_Close, Portfolios_Archive). A proposed portfolio holds no programs, projects or strategic initiatives, which are created only once it is active. Requires the delete permission only, not delivery leadership. Confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Portfolios_Delete", "Delete portfolio")]
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
    [EndpointSummary("Assign a scoring model to a portfolio, enabling project scoring.")]
    [EndpointDescription(
        "Replaces any model already assigned; assigning the current model again changes nothing. Only an Active scoring model can be assigned, and an archived portfolio cannot be changed. Existing project scores keep the model they were recorded with, so projects scored under the previous model drop off Portfolios_GetRankingScoreboard until re-scored. Requires delivery leadership — the caller must be an Owner or Manager of the portfolio; a permission claim alone is not enough. Changes how every project in the portfolio is scored, so confirm with the user before calling. Takes the portfolio's UUID only, not a key.")]
    [McpTool("Portfolios_AssignScoringModel", "Assign portfolio scoring model")]
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
    [EndpointSummary("Clear a portfolio's scoring model, disabling new project scoring.")]
    [EndpointDescription(
        "Recorded scores are kept, but no project in the portfolio can be scored until a model is assigned again (Portfolios_AssignScoringModel). Clearing a portfolio with no model changes nothing. An archived portfolio cannot be changed. Requires delivery leadership — the caller must be an Owner or Manager of the portfolio; a permission claim alone is not enough. Confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Portfolios_ClearScoringModel", "Clear portfolio scoring model")]
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
    [EndpointSummary("Reposition an ordered batch of projects within the portfolio's ranking.")]
    [EndpointDescription(
        "Read the current order first with Portfolios_GetPortfolioProjects (`position` is the 1-based rank). `projectIds` are the projects to move, in the order they should end up; every one must belong to this portfolio. Place them with anchors: `afterProjectId` is the project that will sit immediately above the batch, `beforeProjectId` the one immediately below. Give both to drop between two projects, only `beforeProjectId` (the current first project) to move to the top, or only `afterProjectId` (the current last project) to move to the bottom. At least one anchor is required, an anchor cannot be in the batch, and `afterProjectId` must rank above `beforeProjectId`. Projects already between the anchors keep their order after the batch. Ranks are opaque fractional values; never show them to the user. Requires delivery leadership — the caller must be an Owner or Manager of the portfolio; a permission claim alone is not enough. The ranking is shared, so confirm the new order with the user before calling. Takes the portfolio's UUID only, not a key.")]
    [McpTool("Portfolios_MoveProjectRanks", "Move projects in portfolio ranking")]
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
    [EndpointSummary("Rebalance the portfolio's project ranks to clean, gap-free whole numbers.")]
    [EndpointDescription(
        "Maintenance only: every project keeps its place in the order, closed projects included; only the underlying rank values are respaced. It is never needed to reorder projects — use Portfolios_MoveProjectRanks for that. An archived portfolio cannot be changed. Requires delivery leadership — the caller must be an Owner or Manager of the portfolio; a permission claim alone is not enough. Takes a UUID only, not a key.")]
    [McpTool("Portfolios_RebalanceProjectRanks", "Rebalance portfolio ranking")]
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
    [EndpointSummary(
        "Get the per-project score breakdown behind a portfolio's ranking board: the portfolio's current scoring model definition, plus each project's criterion ratings and output values.")]
    [EndpointDescription(
        "A project's ratings and outputs are empty when it is unscored or its latest score came from a different or older model. Returns the score breakdown only — it does not include project names or rank positions, so pair it with Portfolios_GetPortfolioProjects and join on project ID.")]
    [McpTool("Portfolios_GetRankingScoreboard", "Get portfolio ranking scoreboard")]
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
    [EndpointSummary("Get a list of programs for a portfolio.")]
    [EndpointDescription("Optionally filter by status.")]
    [McpTool("Portfolios_GetPortfolioPrograms", "List a portfolio's programs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProgramListDto>>> GetPrograms(string idOrKey, [FromQuery] ProgramStatus[]? status, CancellationToken cancellationToken)
    {
        var programs = await _dispatcher.Send(new GetProgramsQuery(PortfolioIdOrKey: idOrKey, StatusFilter: status), cancellationToken);

        return programs is not null
            ? Ok(programs)
            : NotFound();
    }

    [HttpGet("{idOrKey}/projects")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get a list of projects for a portfolio.")]
    [EndpointDescription("Optionally filter by status.")]
    [McpTool("Portfolios_GetPortfolioProjects", "List a portfolio's projects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectListDto>>> GetProjects(string idOrKey, [FromQuery] ProjectStatus[]? status, CancellationToken cancellationToken)
    {
        var projects = await _dispatcher.Send(new GetProjectsQuery(StatusFilter: status, PortfolioIdOrKey: idOrKey), cancellationToken);

        return projects is not null
            ? Ok(projects)
            : NotFound();
    }

    [HttpGet("{idOrKey}/strategic-initiatives")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Get a list of strategic initiatives for a portfolio.")]
    [EndpointDescription("Optionally filter by status.")]
    [McpTool("Portfolios_GetPortfolioStrategicInitiatives", "List a portfolio's strategic initiatives")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeListDto>>> GetStrategicInitiatives(string idOrKey, [FromQuery] StrategicInitiativeStatus[]? status, CancellationToken cancellationToken)
    {
        var initiatives = await _dispatcher.Send(new GetStrategicInitiativesQuery(status, idOrKey), cancellationToken);

        return Ok(initiatives);
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Get a list of all project portfolio statuses.")]
    [McpTool("Portfolios_GetPortfolioStatuses", "List portfolio statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioStatusDto>>> GetPortfolioStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetProjectPortfolioStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectPortfolios)]
    [EndpointSummary("Get a lightweight list of project portfolio options (id and name) for use in lookups.")]
    [McpTool("Portfolios_GetPortfolioOptions", "List portfolio options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioOptionDto>>> GetPortfolioOptions(CancellationToken cancellationToken)
    {
        var options = await _dispatcher.Send(new GetProjectPortfolioOptionsQuery(), cancellationToken);

        return Ok(options);
    }
}
