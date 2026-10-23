using CsvHelper;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.Risks.Commands;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Imports;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.Risks;

namespace Wayd.Web.Api.Controllers.Planning;

[Route("api/planning/risks")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Organization, "wayd-teams")]
public class RisksController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly ICsvService _csvService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RisksController(IDispatcher dispatcher, ICsvService csvService, IDateTimeProvider dateTimeProvider)
    {
        _dispatcher = dispatcher;
        _csvService = csvService;
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [EndpointSummary("Get the risks of every team and team of teams.")]
    [EndpointDescription("Open risks only unless `includeClosed` is true. Each has its `id`, `key`, summary, owning `team`, status, ROAM category, `exposure` (Low, Medium or High, from impact and likelihood), assignee and follow-up date. For one team use Teams_GetRisks or TeamsOfTeams_GetRisks; for a planning interval, PlanningIntervals_GetRisks; for the caller's own, Risks_GetMyRisks.")]
    [McpTool("Risks_GetList", "List risks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskListDto>>> GetList(CancellationToken cancellationToken, bool includeClosed = false)
    {
        var risks = await _dispatcher.Send(new GetRisksQuery(includeClosed), cancellationToken);
        return Ok(risks);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [EndpointSummary("Get risk details using the Id or key.")]
    [EndpointDescription("Takes the risk's UUID or integer key. Returns its description, owning team, reporter, `response` (what is being done about it), follow-up and closed dates. Status, category, impact, likelihood and exposure each come as `{ id, name }`; the `id` is what Risks_Update takes.")]
    [McpTool("Risks_GetRisk", "Get risk")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskDetailsDto>> GetRisk(string idOrKey, CancellationToken cancellationToken)
    {
        var risk = await _dispatcher.Send(new GetRiskQuery(idOrKey), cancellationToken);

        return risk is not null
            ? Ok(risk)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [EndpointSummary("Get a risk's activity history, newest first: its creation, detail changes, category, assessment (impact and likelihood), assignee and follow-up date changes, and closing or reopening.")]
    [EndpointDescription("Takes the risk's UUID or integer key. Each entry has a `category`, an `actorKind` with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string, carrying the value before and after a change. People in a payload are employee ids. Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("Risks_GetActivities", "Get risk activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetRiskActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("me")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [EndpointSummary("Get a list of open risks assigned to me.")]
    [EndpointDescription("Scoped to the caller — no user parameter. Open risks whose assignee is the employee linked to the caller's account; empty when the account has no linked employee.")]
    [McpTool("Risks_GetMyRisks", "List my risks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskListDto>>> GetMyRisks(CancellationToken cancellationToken)
    {
        var risks = await _dispatcher.Send(new GetMyRisksQuery(), cancellationToken);
        return Ok(risks);
    }

    [HttpPost()]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Risks)]
    [EndpointSummary("Create a risk for a team or team of teams.")]
    [EndpointDescription("`teamId` is the UUID of the owning team or team of teams (required). `categoryId` is the ROAM category's numeric id from Risks_GetCategories (1 Resolved, 2 Owned, 3 Accepted, 4 Mitigated); `impactId` and `likelihoodId` are grade ids from Risks_GetGrades (1 Low, 2 Medium, 3 High). `assigneeId` is an employee UUID; `followUpDate` is yyyy-MM-dd; `response` is what is being done about it. The risk starts Open, reported now by the caller, whose account must be linked to an employee. Returns the new risk's `id` and `key`.")]
    [McpTool("Risks_Create", "Create risk", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateRiskRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRisk), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Risks)]
    [EndpointSummary("Update a risk, including closing or reopening it.")]
    [EndpointDescription("**This is a whole-record overwrite: an omitted description, assignee, follow-up date or response is cleared.** Read the risk first with Risks_GetRisk and pass back what should stay. Takes the risk's UUID `id`, which must match the body's `riskId`. `teamId` must be the risk's current team; a risk cannot be moved to another team. `statusId` is 1 Open or 2 Closed (Risks_GetStatuses); closing stamps the closed date and reopening clears it. `categoryId`, `impactId` and `likelihoodId` are as for Risks_Create.")]
    [McpTool("Risks_Update", "Update risk")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.RiskId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.RiskId), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Risks)]
    [EndpointSummary("Submit a csv file of risks to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(RiskImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportRiskRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedRisks = _csvService.ReadCsv<ImportRiskRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportRiskDto>> rows = [];
            var validator = new ImportRiskRequestValidator(_dateTimeProvider);
            foreach (var risk in importedRisks)
            {
                // The same key the run will know this row by, so a file without the column still names a
                // row the reader can find rather than an empty id.
                var key = SubmittedImportRow.KeyFor(risk.ImportId, rows.Count + 1);

                var validationResults = await validator.ValidateAsync(risk, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Import Id: {key})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportRiskDto>(risk.ImportId, risk.ToImportRiskDto()));
            }

            var result = await _dispatcher.Send(new ImportRisksCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            // TODO: Convert this to a validation problem details
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [EndpointSummary("Get a list of all risk statuses.")]
    [EndpointDescription("Open and Closed, each with the numeric `id` the risk update tools take as `statusId`.")]
    [McpTool("Risks_GetStatuses", "List risk statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskStatusDto>>> GetStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRiskStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("categories")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [EndpointSummary("Get a list of all risk categories.")]
    [EndpointDescription("The ROAM categories — Resolved, Owned, Accepted, Mitigated — each with the numeric `id` the risk tools take as `categoryId` and a description of when it applies.")]
    [McpTool("Risks_GetCategories", "List risk categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskCategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRiskCategoriesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("grades")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [EndpointSummary("Get a list of all risk grades.")]
    [EndpointDescription("Low, Medium and High, each with the numeric `id` the risk tools take as `impactId` and `likelihoodId`. A risk's exposure is derived from the two: their ids summed below 4 is Low, exactly 4 Medium, above 4 High.")]
    [McpTool("Risks_GetGrades", "List risk grades")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskGradeDto>>> GetGrades(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRiskGradesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }
}
