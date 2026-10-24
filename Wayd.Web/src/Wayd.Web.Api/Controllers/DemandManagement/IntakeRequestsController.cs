using CSharpFunctionalExtensions;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

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
public class IntakeRequestsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Get the intake requests you can see, as summaries.")]
    [EndpointDescription("Requests on a private desk are absent for anyone outside it rather than listed as redacted rows.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeRequestSummaryDto>>> GetIntakeRequests([FromQuery] Guid? deskId, CancellationToken cancellationToken)
    {
        var requests = await _dispatcher.Send(new GetIntakeRequestsQuery(deskId), cancellationToken);
        return Ok(requests);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Get an intake request in the tiers you can read.")]
    [EndpointDescription("The summary is always returned; the substance is null when you may not read it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeRequestDetailsDto>> GetIntakeRequest(string idOrKey, CancellationToken cancellationToken)
    {
        var request = await _dispatcher.Send(new GetIntakeRequestQuery(new IdOrKey(idOrKey)), cancellationToken);
        return request is not null
            ? Ok(request)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Get an intake request's history, limited to the tiers you can read.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetIntakeRequestActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);
        return activities is not null
            ? Ok(activities)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Submit an intake request to a desk.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateIntakeRequestCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIntakeRequest), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Update an intake request.")]
    [EndpointDescription("The requester can edit their own request while it is proposed; otherwise the Update permission is needed. You must also be able to read the request's detail.")]
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

    [HttpPut("{id}/assigned-reviewer")]
    [MustHavePermission(ApplicationAction.Review, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Assign the reviewer who owns an intake request, or unassign it.")]
    [EndpointDescription("Needs Review on the request's desk. The assignee must be a reviewer on that desk.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AssignReviewer(Guid id, [FromBody] AssignIntakeRequestReviewerRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new AssignIntakeRequestReviewerCommand(id, request.ReviewerId), cancellationToken));

    [HttpPost("{id}/participants")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Give an employee access to an intake request as a participant.")]
    [EndpointDescription("A reviewer on the request's desk can always add someone; the requester and submitter can too, except on a private desk that does not allow it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AddParticipant(Guid id, [FromBody] AddIntakeRequestParticipantRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new AddIntakeRequestParticipantCommand(id, request.EmployeeId), cancellationToken));

    [HttpDelete("{id}/participants/{employeeId}")]
    [MustHavePermission(ApplicationAction.Review, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Remove a participant's access to an intake request.")]
    [EndpointDescription("Needs Review on the request's desk.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveParticipant(Guid id, Guid employeeId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new RemoveIntakeRequestParticipantCommand(id, employeeId), cancellationToken));

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Delete an intake request.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new DeleteIntakeRequestCommand(id), cancellationToken));

    [HttpGet("need-by-bases")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Get the need-by basis values, with their codes and families.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NeedByBasisDto>>> GetNeedByBases(CancellationToken cancellationToken)
    {
        var bases = await _dispatcher.Send(new GetNeedByBasesQuery(), cancellationToken);
        return Ok(bases);
    }

    private ActionResult NoContentOrError(Result result) =>
        result.IsSuccess
            ? NoContent()
            : result.Error == IntakeRequestAuthorization.NotFoundError
                ? NotFound()
                : BadRequest(result.ToBadRequestObject(HttpContext));
}
