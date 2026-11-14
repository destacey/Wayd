using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Incidents.Dtos;
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
public class IncidentsController(IDispatcher dispatcher, IWorkDbContext workDbContext) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IWorkDbContext _workDbContext = workDbContext;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("List incidents, highest key first.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<IncidentSummaryDto>>> GetIncidents(
        [FromQuery] int page = PageRequest.FirstPage,
        [FromQuery] int pageSize = PageSizes.Default,
        [FromQuery] ProductStatusAlias[]? status = null,
        [FromQuery] Guid[]? severityLevelId = null,
        CancellationToken cancellationToken = default)
    {
        var incidents = await _dispatcher.Send(
            new GetIncidentsQuery(new PageRequest(page, pageSize), status, severityLevelId),
            cancellationToken);

        return Ok(incidents);
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
