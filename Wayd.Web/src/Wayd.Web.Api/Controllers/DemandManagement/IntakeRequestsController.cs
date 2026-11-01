using Microsoft.AspNetCore.Authorization;
using CsvHelper;
using CSharpFunctionalExtensions;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Imports;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequests;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// Intake requests: asks for delivery capacity that are not yet projects or work items.
/// </summary>
/// <remarks>
/// Every read and command applies the request's read tiers. A request the caller may not see answers 404,
/// exactly like one that does not exist, so a private desk's requests cannot be found by probing.
/// </remarks>
[Route("api/demand-management/intake-requests")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
[McpTools(McpToolset.Intake, "wayd-intake")]
public class IntakeRequestsController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [Authorize]
    [McpTool("IntakeRequests_GetList", "List intake requests")]
    [EndpointSummary("Get the intake requests you can see, as summaries.")]
    [EndpointDescription("Without View, only requests on desks you are a member of and requests that are your own. Requests on a private desk are absent for anyone outside it rather than listed as redacted rows. With `mine`, only requests you are the requester of or submitted, including those entered on someone else's behalf. With `openOnly`, only requests still under review or accepted — in the Proposed or Active status category, whatever the desk's workflow calls the status.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeRequestSummaryDto>>> GetIntakeRequests([FromQuery] Guid? deskId, [FromQuery] bool mine = false, [FromQuery] bool openOnly = false, CancellationToken cancellationToken = default)
    {
        var requests = await _dispatcher.Send(new GetIntakeRequestsQuery(deskId, mine, openOnly), cancellationToken);
        return Ok(requests);
    }

    [HttpGet("scores")]
    [Authorize]
    [McpTool("IntakeRequests_GetScores", "List intake request scores")]
    [EndpointSummary("Get the current scores of the intake requests you can see.")]
    [EndpointDescription("Takes the same filters as the list and answers beside it, for the requests whose score you may read: a score is substance, so a request whose substance is withheld from you is absent, as is one never scored. `isDeskModel` is false for a score from a model the request's desk no longer scores with, which does not rank with the rest.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeRequestCurrentScoreDto>>> GetIntakeRequestScores([FromQuery] Guid? deskId, [FromQuery] bool mine = false, [FromQuery] bool openOnly = false, CancellationToken cancellationToken = default)
    {
        var scores = await _dispatcher.Send(new GetIntakeRequestScoresQuery(deskId, mine, openOnly), cancellationToken);
        return Ok(scores);
    }

    [HttpGet("{idOrKey}")]
    [Authorize]
    [McpTool("IntakeRequests_GetIntakeRequest", "Get intake request")]
    [EndpointSummary("Get an intake request in the tiers you can read.")]
    [EndpointDescription("The summary is always returned; the substance is null when you may not read it. The outcome is the summary's status and its category, with the substance's `statusReason` saying why the desk declined, deferred, finished or marked it a duplicate, or why its requester withdrew it, and `duplicateOf` naming the request it gave way to when you can see that one.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeRequestDetailsDto>> GetIntakeRequest(string idOrKey, CancellationToken cancellationToken)
    {
        var request = await _dispatcher.Send(new GetIntakeRequestQuery(new IdOrKey(idOrKey)), cancellationToken);
        return request is not null
            ? Ok(await WithWorkItems(request, cancellationToken))
            : NotFound();
    }

    /// <summary>
    /// Fills in each work item outcome's current key, title and status from Work, in one call. Demand Management
    /// keeps no copy of work items; a work item Work no longer has keeps its outcome, with no work item.
    /// </summary>
    private async Task<IntakeRequestDetailsDto> WithWorkItems(IntakeRequestDetailsDto request, CancellationToken cancellationToken)
    {
        var workItemIds = request.Outcomes
            .Where(o => o.Kind == IntakeRequestOutcomeKind.WorkItem)
            .Select(o => o.TargetId)
            .ToList();
        if (workItemIds.Count == 0)
            return request;

        var workItems = (await _dispatcher.Send(new GetWorkItemsByIdsQuery(workItemIds), cancellationToken))
            .ToDictionary(w => w.Id, ToOutcomeWorkItem);

        return request with
        {
            Outcomes =
            [
                .. request.Outcomes.Select(o => o.Kind == IntakeRequestOutcomeKind.WorkItem && workItems.TryGetValue(o.TargetId, out var workItem)
                    ? o with { WorkItem = workItem }
                    : o),
            ],
        };
    }

    /// <summary>A work item as Work reports it, in the shape Demand Management takes it.</summary>
    private static IntakeRequestOutcomeWorkItemDto ToOutcomeWorkItem(WorkItemDetailsNavigationDto workItem) => new()
    {
        Id = workItem.Id,
        Key = workItem.Key,
        Title = workItem.Title,
        WorkspaceKey = workItem.WorkspaceKey,
        Status = workItem.Status,
        StatusCategory = (StatusCategory)workItem.StatusCategory.Id,
    };

    [HttpGet("{idOrKey}/activities")]
    [McpTool("IntakeRequests_GetActivities", "Get intake request activity history")]
    [Authorize]
    [EndpointSummary("Get an intake request's history, limited to the tiers you can read.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetIntakeRequestActivitiesQuery(new IdOrKey(idOrKey), new PageRequest(page, pageSize)), cancellationToken);
        return activities is not null
            ? Ok(activities)
            : NotFound();
    }

    [HttpPost]
    [Authorize]
    [McpTool("IntakeRequests_Submit", "Submit intake request", Destructive = false)]
    [EndpointSummary("Submit an intake request to a desk.")]
    [EndpointDescription("Needs your account to be linked to an employee, who is recorded as the submitter, and as the requester unless `requesterId` names someone else. The request starts in its desk's initial status.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateIntakeRequestCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIntakeRequest), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary(
        "Submit a csv file of intake requests to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [EndpointDescription(
        "Each row names its desk, channel, tags and people by id. A request is created on its desk in the status meaning Submitted, at `SubmittedAt`, then assigned, tagged and moved to `Status` — a status of the desk's workflow by name or by meaning — at `StatusChangedAt`. Withdrawn is recorded as the requester's own move; every other status as the assignee's. Needs the Demand Management Admin.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(IntakeRequestImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportIntakeRequestRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var imported = _csvService.ReadCsv<ImportIntakeRequestRequest>(file.OpenReadStream()).ToList();

            List<SubmittedImportRow<ImportIntakeRequestDto>> rows = [];
            var validator = new ImportIntakeRequestRequestValidator();
            for (var i = 0; i < imported.Count; i++)
            {
                var request = imported[i];

                var validationResults = await validator.ValidateAsync(request, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Row: {SubmittedImportRow.KeyFor(request.ImportId, i + 1)})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportIntakeRequestDto>(request.ImportId, request.ToImportIntakeRequestDto()));
            }

            var result = await _dispatcher.Send(new ImportIntakeRequestsCommand(rows, submissionGroupId, validateOnly), cancellationToken);

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
    [McpTool("IntakeRequests_Update", "Update intake request")]
    [Authorize]
    [EndpointSummary("Update an intake request.")]
    [EndpointDescription("The requester can edit their own request while it is proposed; otherwise you must be an agent on the request's desk. You must also be able to read the request's detail.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        return NoContentOrError(await _dispatcher.Send(request.ToUpdateIntakeRequestCommand(), cancellationToken));
    }

    [HttpPut("{id}/assignee")]
    [McpTool("IntakeRequests_ChangeAssignee", "Change intake request assignee")]
    [Authorize]
    [EndpointSummary("Give an intake request a different assignee, or unassign it.")]
    [EndpointDescription("Needs an agent on the request's desk. The assignee must be a member of that desk.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeAssignee(Guid id, [FromBody] ChangeIntakeRequestAssigneeRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new ChangeIntakeRequestAssigneeCommand(id, request.AssigneeId), cancellationToken));

    [HttpPost("{id}/reroute")]
    [McpTool("IntakeRequests_Reroute", "Move intake request to another desk")]
    [Authorize]
    [EndpointSummary("Move an open intake request to another desk.")]
    [EndpointDescription("Needs an agent on the request's current desk; the destination need not be one you work, but must be open to new requests. A move to a less private desk is refused unless `confirmWiderVisibility` is set. An assignee who is not a member of the destination is unassigned.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reroute(Guid id, [FromBody] RerouteIntakeRequestRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new RerouteIntakeRequestCommand(id, request.DeskId, request.ConfirmWiderVisibility, request.StatusId), cancellationToken));

    [HttpGet("{id}/arrival-statuses")]
    [McpTool("IntakeRequests_GetArrivalStatuses", "List intake request arrival statuses")]
    [Authorize]
    [EndpointSummary("Get the statuses an intake request can arrive in on another desk.")]
    [EndpointDescription("The destination workflow's open statuses that need no reason, in its order, and the one the request arrives in unless you choose. Empty unless you can move the request: an agent on its current desk.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeRequestArrivalStatusesDto>> GetArrivalStatuses(Guid id, [FromQuery] Guid deskId, CancellationToken cancellationToken)
    {
        var arrival = await _dispatcher.Send(new GetIntakeRequestArrivalStatusesQuery(id, deskId), cancellationToken);
        return arrival is not null
            ? Ok(arrival)
            : NotFound();
    }

    [HttpPost("{id}/status")]
    [McpTool("IntakeRequests_ChangeStatus", "Change intake request status")]
    [Authorize]
    [EndpointSummary("Move an intake request to another status of its desk's workflow.")]
    [EndpointDescription("Needs an agent on the request's desk. Declining, deferring, marking a duplicate and finishing need a `reason` the requester can read; marking a duplicate needs `duplicateOfId`, a request you can see that is not itself a duplicate. Withdrawn is the requester's, through withdraw. A `reply` is posted in the request's conversation with the move and saved with it: ask the requester a question and move to Waiting on Requester in one call.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeStatus(Guid id, [FromBody] ChangeIntakeRequestStatusRequest request, CancellationToken cancellationToken)
    {
        var reply = request.Reply is { } r ? new IntakeRequestReply(r.Subject, r.Body) : null;
        var result = await _dispatcher.Send(
            new ChangeIntakeRequestStatusCommand(id, request.StatusId, request.Reason, request.DuplicateOfId, reply), cancellationToken);

        return result.IsFailure && DiscussionAccessErrors.IsConflict(result.Error)
            ? Conflict(ProblemDetailsExtensions.ForConflict(result.Error, HttpContext))
            : NoContentOrError(result);
    }

    [HttpPost("{id}/withdraw")]
    [McpTool("IntakeRequests_Withdraw", "Withdraw intake request")]
    [Authorize]
    [EndpointSummary("Withdraw your own open intake request.")]
    [EndpointDescription("Only the request's requester; the submitter cannot withdraw a request entered on someone's behalf.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Withdraw(Guid id, [FromBody] WithdrawIntakeRequestRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new WithdrawIntakeRequestCommand(id, request.Note), cancellationToken));

    [HttpGet("{id}/status-options")]
    [McpTool("IntakeRequests_GetStatusOptions", "List intake request status options")]
    [Authorize]
    [EndpointSummary("Get the statuses you can move an intake request to, with what each move needs.")]
    [EndpointDescription("In its desk workflow's order, without the status it holds or Withdrawn. Empty unless you are an agent on the request's desk.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<IntakeRequestStatusOptionDto>>> GetStatusOptions(Guid id, CancellationToken cancellationToken)
    {
        var options = await _dispatcher.Send(new GetIntakeRequestStatusOptionsQuery(id), cancellationToken);
        return options is not null
            ? Ok(options)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [Authorize]
    [McpTool("IntakeRequests_GetStatusHistory", "Get intake request status history")]
    [EndpointSummary("Get an intake request's status changes, newest first, in the tiers you can read.")]
    [EndpointDescription("Each change reports the status names as they were at the time. Without the request's substance, each change comes without its reason or who made it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusTransitionDto>>> GetStatusHistory(string idOrKey, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetIntakeRequestStatusHistoryQuery(new IdOrKey(idOrKey)), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-workflow")]
    [McpTool("IntakeRequests_GetStatusWorkflow", "Get intake request status workflow")]
    [Authorize]
    [EndpointSummary("Get the status workflow an intake request moves through.")]
    [EndpointDescription("Readable by anyone who can see the request, so its status history can be drawn on the workflow without workflow administration permissions.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatusWorkflowDetailsDto>> GetStatusWorkflow(string idOrKey, CancellationToken cancellationToken)
    {
        var workflow = await _dispatcher.Send(new GetIntakeRequestStatusWorkflowQuery(new IdOrKey(idOrKey)), cancellationToken);
        return workflow is not null
            ? Ok(workflow)
            : NotFound();
    }

    [HttpPost("{id}/participants")]
    [McpTool("IntakeRequests_AddParticipant", "Add intake request participant")]
    [Authorize]
    [EndpointSummary("Give an employee access to an intake request as a participant.")]
    [EndpointDescription("An agent on the request's desk can always add someone; the requester and submitter can too, except on a private desk that does not allow it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AddParticipant(Guid id, [FromBody] AddIntakeRequestParticipantRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new AddIntakeRequestParticipantCommand(id, request.EmployeeId), cancellationToken));

    [HttpDelete("{id}/participants/{employeeId}")]
    [McpTool("IntakeRequests_RemoveParticipant", "Remove intake request participant")]
    [Authorize]
    [EndpointSummary("Remove a participant's access to an intake request.")]
    [EndpointDescription("Needs an agent on the request's desk.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveParticipant(Guid id, Guid employeeId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new RemoveIntakeRequestParticipantCommand(id, employeeId), cancellationToken));

    [HttpPost("{id}/outcomes")]
    [McpTool("IntakeRequests_AddOutcome", "Add intake request outcome", Destructive = false)]
    [Authorize]
    [EndpointSummary("Record that an accepted intake request became a project or work item.")]
    [EndpointDescription("Needs an agent on the request's desk, while the request is accepted (Active category). `NewProject` is a project started for the request; `ExistingProjectScope` is a project that already existed and absorbed it, and needs a note saying what it took on; both take `projectId`. `WorkItem` is a work item that delivers the request and takes `workItemId`; its note is optional. The project or work item itself is not changed. A request points at a project or work item at most once. Once a request has an outcome, finishing it needs no reason.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AddOutcome(Guid id, [FromBody] AddIntakeRequestOutcomeRequest request, CancellationToken cancellationToken)
    {
        if (request.Kind != IntakeRequestOutcomeKind.WorkItem)
            return NoContentOrError(await _dispatcher.Send(
                new AddIntakeRequestOutcomeCommand(id, request.Kind, request.ProjectId ?? Guid.Empty, request.Note), cancellationToken));

        // Demand Management never reads Work's tables, so the work item is looked up here and handed in.
        var workItemId = request.WorkItemId ?? Guid.Empty;
        var found = await _dispatcher.Send(new GetWorkItemsByIdsQuery([workItemId]), cancellationToken);

        return NoContentOrError(await _dispatcher.Send(
            new AddIntakeRequestOutcomeCommand(id, request.Kind, workItemId, request.Note)
            {
                WorkItem = found.Select(ToOutcomeWorkItem).SingleOrDefault(),
            },
            cancellationToken));
    }

    [HttpDelete("{id}/outcomes/{targetId}")]
    [McpTool("IntakeRequests_RemoveOutcome", "Remove intake request outcome")]
    [Authorize]
    [EndpointSummary("Take an outcome off an accepted intake request.")]
    [EndpointDescription("Needs an agent on the request's desk, while the request is accepted. `targetId` is the project or work item the outcome points at. The project or work item itself is not changed.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveOutcome(Guid id, Guid targetId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new RemoveIntakeRequestOutcomeCommand(id, targetId), cancellationToken));

    [HttpGet("projects/{projectId}")]
    [Authorize]
    [McpTool("IntakeRequests_GetProjectIntakeRequests", "List a project's intake requests")]
    [EndpointSummary("Get the intake requests a project came from or absorbed, that you can see.")]
    [EndpointDescription("Ordered by request number. Each request is read under its desk's visibility, so a request on a private desk you are not part of is absent rather than listed as redacted.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectIntakeRequestDto>>> GetProjectIntakeRequests(Guid projectId, CancellationToken cancellationToken)
    {
        var requests = await _dispatcher.Send(new GetProjectIntakeRequestsQuery(projectId), cancellationToken);
        return Ok(requests);
    }

    [HttpGet("work-items/{workItemId}")]
    [Authorize]
    [McpTool("IntakeRequests_GetWorkItemIntakeRequests", "List a work item's intake requests")]
    [EndpointSummary("Get the intake requests a work item delivers, that you can see.")]
    [EndpointDescription("Ordered by request number. Each request is read under its desk's visibility, so a request on a private desk you are not part of is absent rather than listed as redacted.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<WorkItemIntakeRequestDto>>> GetWorkItemIntakeRequests(Guid workItemId, CancellationToken cancellationToken)
    {
        var requests = await _dispatcher.Send(new GetWorkItemIntakeRequestsQuery(workItemId), cancellationToken);
        return Ok(requests);
    }

    [HttpPut("{id}/tags/{tagId}")]
    [McpTool("IntakeRequests_Tag", "Tag intake request")]
    [Authorize]
    [EndpointSummary("File an intake request under a tag.")]
    [EndpointDescription("Needs an agent on the request's desk. The tag must be active, on an active axis the desk offers. On an axis that takes one tag, this replaces the tag the request held there. Applying a tag the request already carries changes nothing.\n\nA free tag — one on no axis, from `IntakeRequestTags_Search` with `isFree` — can be applied too, on a desk that allows free tags. To apply one by name, creating it if needed, use `IntakeRequests_TagByName`.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Tag(Guid id, Guid tagId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new TagIntakeRequestCommand(id, tagId), cancellationToken));

    [HttpPost("{id}/tags")]
    [McpTool("IntakeRequests_TagByName", "Tag intake request by name")]
    [Authorize]
    [EndpointSummary("File an intake request under a typed name, creating a free tag when no tag has it.")]
    [EndpointDescription("Needs an agent on the request's desk. Returns the id of the tag applied. **When exactly one active tag on an axis the desk offers has the name, ignoring case, that tag is applied** — with the same single-value replacement as `IntakeRequests_Tag`. When more than one such axis has it the call is refused: apply the one meant with `IntakeRequests_Tag`.\n\nOtherwise the free tag with the name is applied — a tag on no axis — and created when none has it yet. That needs a desk that allows free tags (`allowsFreeTags` on the desk); otherwise this is refused. Names are trimmed and runs of whitespace collapsed, at most 64 characters.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> TagByName(Guid id, [FromBody] TagIntakeRequestByNameRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new TagIntakeRequestByNameCommand(id, request.Name), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : ErrorResult(result);
    }

    [HttpDelete("{id}/tags/{tagId}")]
    [McpTool("IntakeRequests_Untag", "Untag intake request")]
    [Authorize]
    [EndpointSummary("Take a tag off an intake request.")]
    [EndpointDescription("Needs an agent on the request's desk. Works for an inactive tag too. Removing a tag the request does not carry changes nothing. A free tag no other request carries is deleted with it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Untag(Guid id, Guid tagId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new UntagIntakeRequestCommand(id, tagId), cancellationToken));

    [HttpDelete("{id}")]
    [McpTool("IntakeRequests_Delete", "Delete intake request")]
    [Authorize]
    [EndpointSummary("Delete an intake request.")]
    [EndpointDescription("Needs a Desk Admin on the request's desk. Deleting is a last resort: withdrawing or declining keeps a request on record.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new DeleteIntakeRequestCommand(id), cancellationToken));

    [HttpGet("need-by-bases")]
    [Authorize]
    [McpTool("IntakeRequests_GetNeedByBases", "List need-by bases")]
    [EndpointSummary("Get the need-by basis values, with their codes and families.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NeedByBasisDto>>> GetNeedByBases(CancellationToken cancellationToken)
    {
        var bases = await _dispatcher.Send(new GetNeedByBasesQuery(), cancellationToken);
        return Ok(bases);
    }

    [HttpGet("need-by-basis-families")]
    [McpTool("IntakeRequests_GetNeedByBasisFamilies", "List need-by basis families")]
    [Authorize]
    [EndpointSummary("Get the need-by basis families, with their codes and names.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NeedByBasisFamilyDto>>> GetNeedByBasisFamilies(CancellationToken cancellationToken)
    {
        var families = await _dispatcher.Send(new GetNeedByBasisFamiliesQuery(), cancellationToken);
        return Ok(families);
    }

    [HttpGet("{id}/scoring")]
    [Authorize]
    [McpTool("IntakeRequests_GetScoring", "Get intake request scoring")]
    [EndpointSummary("Get an intake request's scoring: its desk's model, its score history and its rank on the desk.")]
    [EndpointDescription("Answers 404 when you may not read the request's substance, the tier its score belongs to. `scoringModel` is null when the desk does not score its requests. `deskRank` places the request among the open requests on its desk scored with the desk's current model whose scores you may read.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeRequestScoringDto>> GetScoring(Guid id, CancellationToken cancellationToken)
    {
        var scoring = await _dispatcher.Send(new GetIntakeRequestScoringQuery(id), cancellationToken);
        return scoring is not null
            ? Ok(scoring)
            : NotFound();
    }

    [HttpPost("{id}/scores")]
    [Authorize]
    [McpTool("IntakeRequests_Score", "Score intake request", Destructive = false)]
    [EndpointSummary("Score an intake request with its desk's scoring model.")]
    [EndpointDescription("Needs an agent on the request's desk, an open request, and a desk with a scoring model. Rate every criterion: choose a level for one rated on a scale, a number for one without. Adds a score to the request's history and makes it current; earlier scores are kept. Answers the new score's id.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> Score(Guid id, [FromBody] ScoreIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RecordIntakeRequestScoreCommand(id, request.ToInputs()), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScoring), new { id }, result.Value)
            : ErrorResult(result);
    }

    [HttpPost("{id}/scores/preview")]
    [Authorize]
    [McpTool("IntakeRequests_PreviewScore", "Preview intake request score", Destructive = false, Idempotent = true)]
    [EndpointSummary("Preview what ratings would score an intake request, without recording it.")]
    [EndpointDescription("Needs an agent on the request's desk. Calculates every output of the desk's scoring model from the ratings and records nothing.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScoringModelEvaluationDto>> PreviewScore(Guid id, [FromBody] ScoreIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new PreviewIntakeRequestScoreQuery(id, request.ToInputs()), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ErrorResult(result);
    }

    private ActionResult ErrorResult(Result result) =>
        result.Error is IntakeRequestAuthorization.NotFoundError
            ? NotFound()
            : BadRequest(result.ToBadRequestObject(HttpContext));

    private ActionResult NoContentOrError(Result result) =>
        result.IsSuccess
            ? NoContent()
            : result.Error is IntakeRequestAuthorization.NotFoundError or IntakeDeskAccess.NotFoundError
                or IntakeRequestTagCategoryErrors.TagNotFoundError
                ? NotFound()
                : BadRequest(result.ToBadRequestObject(HttpContext));
}
