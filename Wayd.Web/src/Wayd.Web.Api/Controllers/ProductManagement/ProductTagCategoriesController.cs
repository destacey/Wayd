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
    [OpenApiOperation(
        "List the tag categories and the tags in each.",
        "**Call this before Products_Tag**, which needs a tag UUID.\n\nA category is an axis — Platform, Tech Stack, Compliance — and its `allowsMany` flag decides how tagging behaves. On an axis where `allowsMany` is false, applying a second tag **silently replaces** the first rather than refusing, so check this before tagging if the existing value matters.\n\nOnly active tags in active categories can be applied.")]
    [McpTool("ProductTagCategories_GetProductTagCategories", "List product tag categories")]
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
    [OpenApiOperation(
        "Create a tag category — an axis such as Platform, Tech Stack or Compliance.",
        "**`allowsMany` cannot be changed afterwards**, so choose it deliberately: it decides whether a product may carry several tags on this axis, or whether applying a second one silently replaces the first. Names must be unique. The category is created empty; add tags with ProductTagCategories_AddTag.")]
    [McpTool("ProductTagCategories_Create", "Create a tag category", Destructive = false)]
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
    [OpenApiOperation(
        "Rename a tag category or change its description.",
        "**An omitted description is cleared.** `allowsMany` is not here and cannot be changed after creation. Refused on a seeded system category. Names must stay unique.")]
    [McpTool("ProductTagCategories_Update", "Update a tag category", Idempotent = false)]
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
    [OpenApiOperation(
        "Take a tag category out of use, or put it back.",
        "Tags on an inactive category cannot be applied to a product, though products already carrying them keep them and can still have them removed.\n\nAs with product types, this **is** allowed on a seeded system category, unlike editing.")]
    [McpTool("ProductTagCategories_SetActive", "Activate or deactivate a tag category", Idempotent = false)]
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
    [OpenApiOperation(
        "Put the tag categories in a given order.",
        "**The list must name every category exactly once** — a partial list is refused, so read them all first and send the complete sequence. Ordering is presentation only.")]
    [McpTool("ProductTagCategories_Reorder", "Reorder tag categories", Idempotent = false)]
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
    [OpenApiOperation(
        "Delete a tag category and its tags.",
        "Seeded system records cannot be modified or deleted, and a record in use cannot be deleted — deactivate it instead, which stops new use without breaking what already refers to it. A category counts as in use when any product is tagged along it, so this only removes an axis created by mistake and never applied.")]
    [McpTool("ProductTagCategories_Delete", "Delete a tag category", Idempotent = false)]
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
    [OpenApiOperation(
        "Add a tag to a category.",
        "Tag names must be unique within their axis, though the same name may appear on different axes. Refused on a seeded system category.")]
    [McpTool("ProductTagCategories_AddTag", "Add a tag", Destructive = false)]
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
    [OpenApiOperation(
        "Rename a tag or change its description.",
        "**An omitted description is cleared.** The tag must belong to the category named in the path. Names must stay unique within the axis. Refused on a seeded system category.\n\nRenaming does not rewrite history: a product carrying the tag simply reports the new name.")]
    [McpTool("ProductTagCategories_RenameTag", "Rename a tag", Idempotent = false)]
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
    [OpenApiOperation(
        "Take a tag out of use, or put it back.",
        "An inactive tag cannot be applied to a product, though products already carrying it keep it and can still have it removed. The tag must belong to the category named in the path.\n\n**Refused on a seeded system category, and here there is no fallback** — unlike a category or a product type, an individual system tag can be neither modified nor retired. Deactivate the whole axis with `ProductTagCategories_SetActive` if it should stop being used.")]
    [McpTool("ProductTagCategories_SetTagActive", "Activate or deactivate a tag", Idempotent = false)]
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

    [HttpDelete("{id}/tags/{tagId}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ProductTagCategories)]
    [OpenApiOperation(
        "Permanently delete a tag.",
        "The tag must belong to the category named in the path. Refused on a seeded system category, and refused while any product carries the tag — deactivate it with `ProductTagCategories_SetTagActive` instead, which stops new use without stripping it from the products that have it. The `productCount` on each tag from `ProductTagCategories_GetProductTagCategories` says whether it is in use.")]
    [McpTool("ProductTagCategories_DeleteTag", "Delete a tag", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteTag(Guid id, Guid tagId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProductTagCommand(id, tagId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
