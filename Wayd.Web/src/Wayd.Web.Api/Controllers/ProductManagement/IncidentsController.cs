using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Application.Incidents.Imports;
using Wayd.ProductManagement.Application.Incidents.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.Incidents;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Operational incidents linked to products in the catalog.
/// </summary>
[Route("api/product-management/incidents")]
[ApiController]
[FeatureGate(RequirementType.All, FeatureFlags.Names.ProductManagement, FeatureFlags.Names.IncidentManagement)]
public class IncidentsController(IDispatcher dispatcher, IWorkDbContext workDbContext, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("List incidents, filtered and sorted, highest key first by default.")]
    [EndpointDescription(
        "One page of the incidents matching the state and severity filters. `filter` and `sort` use Gridify syntax over `key`, `title`, `severity`, `state`, `products`, `releases`, `reportedBy`, `commander` and `noticedAt`: `filter=title=*checkout` (contains), `^` starts with, `$` ends with, `!=` not equal, `field=` blank, `,` and, `|` or, parentheses to group, `\\` before any other character to match it literally. `severity`, `products`, `releases`, `reportedBy` and `commander` match by id (`reportedBy=0196a8d5-…`), `state` by status name, and `noticedAt` takes ISO instants (`noticedAt>=2026-03-01T00:00:00Z`). `products` and `releases` match an incident holding any of the ids, cannot be sorted by, and cannot be compared with blank. `sort=severity asc, noticedAt desc`: severity sorts by its rank and people by name. A field outside that list fails with a 422. The values each filterable column holds come from `GET filter-options`.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<IncidentSummaryDto>>> GetIncidents(
        [FromQuery] int page = PageRequest.FirstPage,
        [FromQuery] int pageSize = PageSizes.Default,
        [FromQuery] ProductStatusAlias[]? status = null,
        [FromQuery] Guid[]? severityLevelId = null,
        [FromQuery] string? filter = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var incidents = await _dispatcher.Send(
            new GetIncidentsQuery(new PageRequest(page, pageSize), status, severityLevelId, filter, sort),
            cancellationToken);

        return Ok(incidents);
    }

    [HttpGet("filter-options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("List the values one incidents column holds, for its filter.")]
    [EndpointDescription(
        "The distinct values `field` holds across every incident, each with the `value` to send as `field=value` in the list's `filter` and the `label` to show for it. `field` is one of `severity` (in rank order), `state`, `products`, `releases`, `reportedBy` or `commander`; the others are in label order. `search` keeps the labels containing it, ignoring case. At most `limit` values (1 to 200, 100 by default) are returned; `hasMore` is true when more match, so narrow them with `search`. An unknown field fails with a 422.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ListFilterOptionsDto>> GetFilterOptions(
        [FromQuery] string? field = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var options = await _dispatcher.Send(
            new GetIncidentFilterOptionsQuery(field, search, limit),
            cancellationToken);

        return Ok(options);
    }

    [HttpGet("figures")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("Figures for the filtered incidents.")]
    [EndpointDescription(
        "The active count, the longest active incident, and the mean time to recover describe every incident matching the state and severity filters, including rows on other pages.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IncidentFiguresDto>> GetIncidentFigures(
        [FromQuery] ProductStatusAlias[]? status = null,
        [FromQuery] Guid[]? severityLevelId = null,
        CancellationToken cancellationToken = default)
    {
        var figures = await _dispatcher.Send(
            new GetIncidentFiguresQuery(status, severityLevelId),
            cancellationToken);

        return Ok(figures);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("Get one incident by id or key.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentDto>> GetIncident(string idOrKey, CancellationToken cancellationToken)
    {
        var incident = await _dispatcher.Send(new GetIncidentQuery(new IdOrKey(idOrKey)), cancellationToken);

        return incident is not null ? Ok(incident) : NotFound();
    }

    [HttpGet("{idOrKey}/status-workflow")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("Get the status workflow an incident moves through.")]
    [EndpointDescription(
        "The workflow the incident holds, with its statuses in the order that workflow lays out. Readable by anyone who can view the incident, so the record can show that order without the settings permission.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatusWorkflowDetailsDto>> GetStatusWorkflow(
        string idOrKey, CancellationToken cancellationToken)
    {
        var workflow = await _dispatcher.Send(
            new GetIncidentStatusWorkflowQuery(new IdOrKey(idOrKey)), cancellationToken);

        return workflow is not null ? Ok(workflow) : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Incidents)]
    [EndpointSummary("Open an incident.")]
    [EndpointDescription(
        "Requires a linked employee, who becomes the commander. NoticedAt defaults to now when omitted.")]
    [ProducesResponseType(typeof(ObjectIdAndKey), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ObjectIdAndKey>> Create(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIncident), new { idOrKey = result.Value.Id }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Incidents)]
    [EndpointSummary(
        "Submit a csv file of incidents to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [EndpointDescription(
        "Each row is created open and then walked to its Status alias: Open, Responding, Resolved, Closed, or Canceled. Severity is a level name in the selected severity set. Products, releases, and employees are ids. Who submitted the incident is the importer's linked employee. A second submission of the same file creates another incident for every row.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(IncidentImportDefinition.ImportKey)]
    public async Task<ActionResult> Import(
        [FromForm, CsvRows(typeof(ImportIncidentRequest))] IFormFile file,
        [FromQuery] Guid? submissionGroupId,
        [FromQuery] bool validateOnly,
        [FromServices] ImportSubmissionResponder responder,
        CancellationToken cancellationToken)
    {
        try
        {
            var importedIncidents = _csvService.ReadCsv<ImportIncidentRequest>(file.OpenReadStream()).ToList();

            List<SubmittedImportRow<ImportIncidentDto>> rows = [];
            var validator = new ImportIncidentRequestValidator();
            for (var i = 0; i < importedIncidents.Count; i++)
            {
                var incident = importedIncidents[i];
                var validationResults = await validator.ValidateAsync(incident, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage =
                            $"{error.ErrorMessage} (Row: {SubmittedImportRow.KeyFor(incident.ImportId, i + 1)})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }

                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportIncidentDto>(incident.ImportId, incident.ToImportIncidentDto()));
            }

            var result = await _dispatcher.Send(
                new ImportIncidentsCommand(rows, submissionGroupId, validateOnly),
                cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvFileException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPut("{id:guid}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Incidents)]
    [EndpointSummary("Update an incident's details.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateDetails(
        Guid id,
        [FromBody] UpdateIncidentDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));
        }

        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}/teams-chat")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Incidents)]
    [EndpointSummary("Link a Teams chat on an incident.")]
    [EndpointDescription("Stores one pasted Teams link, or removes the stored chat when the link is empty. Wayd does not join the chat, look it up, or send it for approval. Linking again replaces the stored chat.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> LinkTeamsChat(
        Guid id,
        [FromBody] LinkIncidentTeamsChatRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));
        }

        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/teams-chat")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Incidents)]
    [EndpointSummary("Create the incident's Teams chat.")]
    [EndpointDescription("Creates one Teams group chat through the single active Teams connector whose configuration is valid, and stores that chat on the incident. The name is the incident key and the incident name. Reported by, submitted by, and the commander are added when they can be found in the tenant. Extra people are included only in this chat and are not stored. Wayd does not post a message and does not send the chat for approval. A connector that cannot create the chat leaves the incident unchanged.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateTeamsChat(
        Guid id,
        [FromBody] CreateIncidentTeamsChatRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
        {
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));
        }

        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("Get work items linked to an incident.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkItemsSummaryDto>> GetIncidentWorkItems(
        string idOrKey,
        CancellationToken cancellationToken)
    {
        var incident = await _dispatcher.Send(new GetIncidentQuery(new IdOrKey(idOrKey)), cancellationToken);
        if (incident is null)
            return NotFound();

        var workItemsSummary = await _dispatcher.Send(
            new GetExternalObjectWorkItemsQuery(incident.Id),
            cancellationToken);

        if (workItemsSummary.WorkItems.Count > 0)
            workItemsSummary.WorkItems = [.. workItemsSummary.WorkItems.OrderBy(w => w.StackRank)];

        return Ok(workItemsSummary);
    }

    [HttpPost("{id:guid}/work-items")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Incidents)]
    [EndpointSummary("Set the complete list of work items linked to an incident.")]
    [EndpointDescription(
        "Replaces the links: `workItemIds` is every work item that should be linked; a linked item left out is unlinked and an empty list unlinks them all. Read the current links with GET work-items first and pass back every one that should stay. `incidentId` in the body must match the route id.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ManageIncidentWorkItems(
        Guid id,
        [FromBody] ManageIncidentWorkItemsRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.IncidentId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var incident = await _dispatcher.Send(new GetIncidentQuery(new IdOrKey(id)), cancellationToken);
        if (incident is null)
            return NotFound();

        var workItemsBefore = await _dispatcher.Send(
            new GetExternalObjectWorkItemsQuery(incident.Id),
            cancellationToken);

        // The link rows and the timeline entry commit together. A timeline failure used to return 400
        // after the links were already saved.
        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);

        var result = await _dispatcher.Send(
            request.ToManageExternalObjectWorkItemsCommand(),
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ToBadRequestObject(HttpContext));
        }

        var beforeById = workItemsBefore.WorkItems.ToDictionary(w => w.Id);
        var beforeIds = beforeById.Keys.ToHashSet();
        var afterIds = request.WorkItemIds.ToHashSet();

        var workItemsAfter = await _dispatcher.Send(
            new GetExternalObjectWorkItemsQuery(incident.Id),
            cancellationToken);
        var afterById = workItemsAfter.WorkItems.ToDictionary(w => w.Id);

        var removedIds = beforeIds.Except(afterIds).ToList();
        var addedIds = afterIds.Except(beforeIds).ToList();

        var changes = new List<IncidentTimelineLinkChangeDto>();

        if (removedIds.Count == 1 && addedIds.Count == 1
            && beforeById.TryGetValue(removedIds[0], out var fromWorkItem)
            && afterById.TryGetValue(addedIds[0], out var toWorkItem))
        {
            changes.Add(new IncidentTimelineLinkChangeDto(
                IncidentTimelineLinkKind.WorkItem,
                IncidentTimelineLinkAction.Updated,
                $"{fromWorkItem.Title} → {toWorkItem.Title}",
                fromWorkItem.Title,
                toWorkItem.Title,
                toWorkItem.Id,
                fromWorkItem.Id));
        }
        else
        {
            foreach (var removedId in removedIds)
            {
                if (beforeById.TryGetValue(removedId, out var removed))
                {
                    changes.Add(new IncidentTimelineLinkChangeDto(
                        IncidentTimelineLinkKind.WorkItem,
                        IncidentTimelineLinkAction.Removed,
                        removed.Title,
                        TargetId: removed.Id));
                }
            }

            foreach (var addedId in addedIds)
            {
                if (afterById.TryGetValue(addedId, out var added))
                {
                    changes.Add(new IncidentTimelineLinkChangeDto(
                        IncidentTimelineLinkKind.WorkItem,
                        IncidentTimelineLinkAction.Added,
                        added.Title,
                        TargetId: added.Id));
                }
            }
        }

        if (changes.Count > 0)
        {
            var timelineResult = await _dispatcher.Send(
                new RecordIncidentTimelineLinkEntriesCommand(incident.Id, changes),
                cancellationToken);

            if (timelineResult.IsFailure)
            {
                return BadRequest(timelineResult.ToBadRequestObject(HttpContext));
            }
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{idOrKey}/timeline-notes")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("List commander timeline notes for an incident.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<IncidentTimelineNoteDto>>> GetIncidentTimelineNotes(
        string idOrKey,
        CancellationToken cancellationToken)
    {
        var incident = await _dispatcher.Send(new GetIncidentQuery(new IdOrKey(idOrKey)), cancellationToken);
        if (incident is null)
            return NotFound();

        var notes = await _dispatcher.Send(
            new GetIncidentTimelineNotesQuery(new IdOrKey(idOrKey)),
            cancellationToken);

        return Ok(notes);
    }

    [HttpGet("{idOrKey}/timeline-link-entries")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("List link change entries on an incident timeline.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<IncidentTimelineLinkEntryDto>>> GetIncidentTimelineLinkEntries(
        string idOrKey,
        CancellationToken cancellationToken)
    {
        var incident = await _dispatcher.Send(new GetIncidentQuery(new IdOrKey(idOrKey)), cancellationToken);
        if (incident is null)
            return NotFound();

        var entries = await _dispatcher.Send(
            new GetIncidentTimelineLinkEntriesQuery(new IdOrKey(idOrKey)),
            cancellationToken);

        return Ok(entries);
    }

    [HttpPost("{id:guid}/timeline-notes")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Incidents)]
    [EndpointSummary("Add a note to an incident's operational timeline.")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> AddIncidentTimelineNote(
        Guid id,
        [FromBody] AddIncidentTimelineNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.IncidentId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var incident = await _dispatcher.Send(new GetIncidentQuery(new IdOrKey(id)), cancellationToken);
        if (incident is null)
            return NotFound();

        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIncidentTimelineNotes), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/workflow-actions")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("List lifecycle actions allowed for an incident from its current status.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<IncidentWorkflowActionDto>>> GetIncidentWorkflowActions(
        string idOrKey,
        CancellationToken cancellationToken)
    {
        var actions = await _dispatcher.Send(
            new GetIncidentWorkflowActionsQuery(new IdOrKey(idOrKey)),
            cancellationToken);

        return actions is null ? NotFound() : Ok(actions);
    }

    [HttpPost("{id:guid}/workflow-actions")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Incidents)]
    [EndpointSummary("Apply a lifecycle action to an incident.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ApplyIncidentWorkflowAction(
        Guid id,
        [FromBody] ApplyIncidentWorkflowActionRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await _dispatcher.Send(new GetIncidentQuery(new IdOrKey(id)), cancellationToken);
        if (incident is null)
            return NotFound();

        var result = await _dispatcher.Send(request.ToCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
