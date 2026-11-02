using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Application.Incidents.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.Incidents;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Operational incidents linked to products in the catalog.
/// </summary>
[Route("api/product-management/incidents")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(RequirementType.All, FeatureFlags.Names.ProductManagement, FeatureFlags.Names.IncidentManagement)]
public class IncidentsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Incidents)]
    [EndpointSummary("List incidents, highest key first.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<IncidentSummaryDto>>> GetIncidents(
        [FromQuery] int page = PageRequest.FirstPage,
        [FromQuery] int pageSize = PageSizes.Default,
        CancellationToken cancellationToken = default)
    {
        var incidents = await _dispatcher.Send(
            new GetIncidentsQuery(new PageRequest(page, pageSize)),
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

        var result = await _dispatcher.Send(
            request.ToManageExternalObjectWorkItemsCommand(),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
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
