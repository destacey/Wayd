using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.ProductTagCategories.Dtos;
using Wayd.ProductManagement.Application.ProductTagCategories.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// The axes products are labelled along — Platform, Tech Stack, Compliance — and the tags on each.
/// </summary>
/// <remarks>
/// Tags are managed through their axis rather than as a resource of their own, because uniqueness
/// within an axis is the axis's rule to enforce: a tag cannot see its siblings.
/// </remarks>
[Route("api/product-management/product-tag-categories")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
public class ProductTagCategoriesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Get a list of tag categories and their tags.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProductTagCategoryDto>>> GetProductTagCategories(
        [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var categories = await _dispatcher.Send(new GetProductTagCategoriesQuery(isActive), cancellationToken);

        return Ok(categories);
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Create a tag category.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create(
        [FromBody] CreateProductTagCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProductTagCategoryCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProductTagCategories), null, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Update a tag category.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(
        Guid id, [FromBody] UpdateProductTagCategoryRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateProductTagCategoryCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/active")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Activate or deactivate a tag category.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SetActive(
        Guid id, [FromBody] SetProductTagCategoryActiveRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToSetProductTagCategoryActiveCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Put the tag categories in a given order.", "Takes the whole set, not a subset.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reorder(
        [FromBody] ReorderProductTagCategoriesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderProductTagCategoriesCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Delete an unused tag category.", "An axis products are tagged along must be deactivated instead.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProductTagCategoryCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/tags")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Add a tag to a category.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> AddTag(
        Guid id, [FromBody] AddProductTagRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new AddProductTagCommand(id, request.Name, request.Description), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/tags/{tagId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Rename a tag.", "Safe on a tag in use: products reference it by id.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> RenameTag(
        Guid id, Guid tagId, [FromBody] RenameProductTagRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new RenameProductTagCommand(id, tagId, request.Name, request.Description), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/tags/{tagId}/active")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation("Activate or deactivate a tag.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetTagActive(
        Guid id, Guid tagId, [FromBody] SetProductTagActiveRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new SetProductTagActiveCommand(id, tagId, request.IsActive), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
