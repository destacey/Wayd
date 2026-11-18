using Microsoft.AspNetCore.Authorization;
using Wayd.Links;
using Wayd.Links.Commands;
using Wayd.Common.Domain.Links;
using Wayd.Links.Models;
using Wayd.Links.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Links;

namespace Wayd.Web.Api.Controllers.Links;

/// <summary>
/// The links on a record — a project, a team, and every kind of record that registers a link owner type.
/// </summary>
/// <remarks>
/// Needs only a signed-in account: every action asks the record's own owner type what the caller may read and change,
/// from the record as it is now. A record the caller cannot read is 404, never 403.
/// </remarks>
[Route("api/links/{ownerType}/{ownerId:guid}")]
[ApiController]
[Authorize]
public class LinksController(IDispatcher dispatcher) : ControllerBase
{
    private const string OwnerDocs =
        "`ownerType` names the kind of record — `organization.team`, `planning.planning-interval`, `planning.planning-interval-objective`, `planning.risk`, `ppm.portfolio`, `ppm.program`, `ppm.project`, `ppm.strategic-initiative`, `product.incident` or `work.sprint` — and `ownerId` is the record's UUID, not its key; an `ownerType` that cannot carry links returns 400. Returns 404 when the record does not exist or you cannot read it.";

    private const string ManageDocs =
        "Needs what editing the record itself needs: its `Update` permission (`Manage` for a planning interval objective); for a project, program or portfolio, delivery leadership as well, and never on an archived portfolio; for a sprint, membership of its team or team of teams, which `Permissions.Iterations.Administer` waives; for an incident, an account linked to an employee. Returns 403 otherwise.";

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [EndpointSummary("Get a record's links.")]
    [EndpointDescription($"{OwnerDocs} Returns the record's links by name, and whether you may change them (`canManage`).")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecordLinksDto>> GetLinks(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetLinksQuery(ownerType, ownerId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(result.Error, HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpGet("{linkId:guid}")]
    [EndpointSummary("Get one of a record's links.")]
    [EndpointDescription($"{OwnerDocs} Returns 404 too when the link is on another record.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LinkDto>> GetLink(string ownerType, Guid ownerId, Guid linkId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetLinkQuery(ownerType, ownerId, linkId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(result.Error, HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    [EndpointSummary("Add a link to a record.")]
    [EndpointDescription($"{OwnerDocs} Returns the new link's id. The address must be a full web address. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> Create(string ownerType, Guid ownerId, [FromBody] LinkRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CreateLinkCommand(ownerType, ownerId, request.Name, request.Url), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetLink), new { ownerType, ownerId, linkId = result.Value }, result.Value)
            : ErrorResult(result.Error);
    }

    [HttpPut("{linkId:guid}")]
    [EndpointSummary("Change a link's name and address.")]
    [EndpointDescription($"{OwnerDocs} Send both fields, including the one that stays. A new address must be a full web address. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(string ownerType, Guid ownerId, Guid linkId, [FromBody] LinkRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UpdateLinkCommand(ownerType, ownerId, linkId, request.Name, request.Url), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpDelete("{linkId:guid}")]
    [EndpointSummary("Remove a link from a record.")]
    [EndpointDescription($"{OwnerDocs} {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Delete(string ownerType, Guid ownerId, Guid linkId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteLinkCommand(ownerType, ownerId, linkId), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    private ActionResult ErrorResult(string error)
    {
        if (LinkAccessErrors.IsNotFound(error))
            return NotFound();

        if (LinkAccessErrors.IsForbidden(error))
            return Problem(detail: error, statusCode: StatusCodes.Status403Forbidden);

        return BadRequest(ProblemDetailsExtensions.ForBadRequest(error, HttpContext));
    }
}
