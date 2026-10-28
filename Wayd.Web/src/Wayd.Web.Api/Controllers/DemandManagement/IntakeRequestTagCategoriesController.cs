using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Dtos;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Queries;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// The tag axes reviewers file intake requests along, and the tags on each, managed by the Demand Management
/// Admin.
/// </summary>
/// <remarks>
/// Tags are managed through their axis rather than as a resource of their own, because uniqueness within an
/// axis is the axis's rule to enforce. Applying a tag to a request is on the intake requests endpoints.
/// </remarks>
[Route("api/demand-management/intake-request-tag-categories")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
public class IntakeRequestTagCategoriesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [Authorize]
    [EndpointSummary("Get the intake request tag axes, each with its tags.")]
    [EndpointDescription("Axes in presentation order, tags alphabetically, inactive tags included. Inactive axes only with `includeInactive`. With `deskId`, only the active axes that desk's agents tag along, which may be none; `includeInactive` is then ignored. Each tag's `requestCount` counts only the requests the caller can see.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeRequestTagCategoryDto>>> GetIntakeRequestTagCategories([FromQuery] bool includeInactive = false, [FromQuery] Guid? deskId = null, CancellationToken cancellationToken = default)
    {
        var categories = await _dispatcher.Send(new GetIntakeRequestTagCategoriesQuery(includeInactive, deskId), cancellationToken);
        return Ok(categories);
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Add an intake request tag axis to the end of the list.")]
    [EndpointDescription("The axis starts with no tags. `allowsMany` cannot be changed afterwards.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateIntakeRequestTagCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateIntakeRequestTagCategoryCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIntakeRequestTagCategories), null, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Rename an intake request tag axis or change its description.")]
    [EndpointDescription("Refused on a system axis.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateIntakeRequestTagCategoryRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        return NoContentOrError(await _dispatcher.Send(request.ToUpdateIntakeRequestTagCategoryCommand(), cancellationToken));
    }

    [HttpPut("{id}/active")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Take an intake request tag axis out of use, or put it back.")]
    [EndpointDescription("Nothing new can be tagged along an inactive axis; requests already tagged keep their tags and can still have them removed.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetActive(Guid id, [FromBody] SetIntakeRequestTagCategoryActiveRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new SetIntakeRequestTagCategoryActiveCommand(id, request.IsActive), cancellationToken));

    [HttpPut("order")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Put the intake request tag axes in the order they are presented.")]
    [EndpointDescription("The list must name every axis, inactive ones included, exactly once; a partial list is refused.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reorder([FromBody] ReorderIntakeRequestTagCategoriesRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(request.ToReorderIntakeRequestTagCategoriesCommand(), cancellationToken));

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Delete an intake request tag axis and its tags.")]
    [EndpointDescription("Refused on a system axis, and while any request is tagged along it; deactivate it instead.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new DeleteIntakeRequestTagCategoryCommand(id), cancellationToken));

    [HttpPost("{id}/tags")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Add a tag to an intake request tag axis.")]
    [EndpointDescription("Tag names are unique within their axis. Refused on a system axis.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> AddTag(Guid id, [FromBody] AddIntakeRequestTagRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new AddIntakeRequestTagCommand(id, request.Name, request.Description), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : ErrorFor(result.Error);
    }

    [HttpPut("{id}/tags/{tagId}")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Rename a tag or change its description.")]
    [EndpointDescription("Every request carrying the tag reads the new name. Refused on a system axis.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateTag(Guid id, Guid tagId, [FromBody] UpdateIntakeRequestTagRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new UpdateIntakeRequestTagCommand(id, tagId, request.Name, request.Description), cancellationToken));

    [HttpPut("{id}/tags/{tagId}/active")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Take a tag out of use, or put it back.")]
    [EndpointDescription("An inactive tag cannot be applied; requests already carrying it keep it and can still have it removed. Refused on a system axis.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetTagActive(Guid id, Guid tagId, [FromBody] SetIntakeRequestTagActiveRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new SetIntakeRequestTagActiveCommand(id, tagId, request.IsActive), cancellationToken));

    [HttpDelete("{id}/tags/{tagId}")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Delete a tag.")]
    [EndpointDescription("Refused on a system axis, and while any request carries the tag; deactivate it instead.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTag(Guid id, Guid tagId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new DeleteIntakeRequestTagCommand(id, tagId), cancellationToken));

    private ActionResult NoContentOrError(Result result) =>
        result.IsSuccess ? NoContent() : ErrorFor(result.Error);

    private ActionResult ErrorFor(string error) =>
        error is IntakeRequestTagCategoryErrors.NotFoundError or IntakeRequestTagCategory.TagNotFoundError
            ? NotFound()
            : BadRequest(Result.Failure(error).ToBadRequestObject(HttpContext));
}
