using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// Intake desks: the review responsibilities requests are submitted to.
/// </summary>
[Route("api/demand-management/intake-desks")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
public class IntakeDesksController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [Authorize]
    [EndpointSummary("Get the intake desks, or only those a requester can submit to now.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeDeskDto>>> GetIntakeDesks([FromQuery] bool submittableOnly = false, CancellationToken cancellationToken = default)
    {
        var desks = await _dispatcher.Send(new GetIntakeDesksQuery(submittableOnly), cancellationToken);
        return Ok(desks);
    }

    [HttpGet("{idOrKey}")]
    [Authorize]
    [EndpointSummary("Get an intake desk.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeDeskDto>> GetIntakeDesk(string idOrKey, CancellationToken cancellationToken)
    {
        var desk = await _dispatcher.Send(new GetIntakeDeskQuery(new IdOrKey(idOrKey)), cancellationToken);
        return desk is not null
            ? Ok(desk)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Create an intake desk with its initial members.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateIntakeDeskRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateIntakeDeskCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIntakeDesk), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [Authorize]
    [EndpointSummary("Update an intake desk's settings and members.")]
    [EndpointDescription("Needs a Desk Admin on the desk. Changing its visibility, or naming, removing or re-roling a Desk Admin, needs the Demand Management Admin. A member removed while owning requests on the desk is unassigned from them.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateIntakeDeskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        return NoContentOrError(await _dispatcher.Send(request.ToUpdateIntakeDeskCommand(), cancellationToken));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Delete an intake desk with no requests on it.")]
    [EndpointDescription("A desk with requests on it cannot be deleted; close it instead.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new DeleteIntakeDeskCommand(id), cancellationToken));

    [HttpGet("visibilities")]
    [Authorize]
    [EndpointSummary("Get the desk visibility values, with their codes.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeDeskVisibilityDto>>> GetVisibilities(CancellationToken cancellationToken)
    {
        var visibilities = await _dispatcher.Send(new GetIntakeDeskVisibilitiesQuery(), cancellationToken);
        return Ok(visibilities);
    }

    [HttpGet("roles")]
    [Authorize]
    [EndpointSummary("Get the roles a desk member can hold, with their codes.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeDeskRoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _dispatcher.Send(new GetIntakeDeskRolesQuery(), cancellationToken);
        return Ok(roles);
    }

    private ActionResult NoContentOrError(Result result) =>
        result.IsSuccess
            ? NoContent()
            : result.Error == IntakeDeskAccess.NotFoundError
                ? NotFound()
                : BadRequest(result.ToBadRequestObject(HttpContext));
}
