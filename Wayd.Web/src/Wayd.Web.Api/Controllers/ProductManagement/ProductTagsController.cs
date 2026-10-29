using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.SystemSettings.ProductTags.Dtos;
using Wayd.Common.Application.SystemSettings.ProductTags.Queries;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.ProductTags.Commands;
using Wayd.ProductManagement.Application.ProductTags.Dtos;
using Wayd.ProductManagement.Application.ProductTags.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.ProductTags;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Product tags across every category, and the free tags — tags on no category — that people add while tagging.
/// </summary>
/// <remarks>
/// A tag on a category is changed through <see cref="ProductTagCategoriesController"/>, because uniqueness within
/// a category is the category's rule. A free tag has no category, so it is changed here.
/// </remarks>
[Route("api/product-management/product-tags")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
[McpTools(McpToolset.Products, "wayd-products")]
public class ProductTagsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProductTagCategories)]
    [EndpointSummary("Search product tags, on categories and free, with how many products carry each.")]
    [EndpointDescription(
        "Sorted by name. `search` keeps tags whose name contains it, ignoring case. `isFree` true returns only free tags — tags on no category, added by typing a name while tagging — and false only tags on categories. A free tag has no `categoryId` or `categoryName`.\n\nTags that cannot be applied — inactive ones, and those on inactive categories — are left out unless `includeInactive` is true.")]
    [McpTool("ProductTags_Search", "Search product tags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductTagListItemDto>>> Search(
        [FromQuery] string? search, [FromQuery] bool? isFree, [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var tags = await _dispatcher.Send(new GetProductTagsQuery(search, isFree, includeInactive), cancellationToken);

        return Ok(tags);
    }

    [HttpGet("settings")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProductTagCategories)]
    [EndpointSummary("Get whether free tags are turned on for products.")]
    [EndpointDescription(
        "Read-only, for whoever tags products: the same `allowsFreeTags` the product tag system settings hold, readable without the system settings permission. Changed through the system settings.")]
    [McpTool("ProductTags_GetSettings", "Get product tag settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductTagSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _dispatcher.Send(new GetProductTagSettingsQuery(), cancellationToken);

        return Ok(settings);
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTagCategories)]
    [EndpointSummary("Rename a free tag or change its description.")]
    [EndpointDescription(
        "**Free tags only**; a tag on a category is renamed with `ProductTagCategories_RenameTag`. **An omitted description is cleared.** Names are unique among free tags, ignoring case — to fold a free tag into one that already has the name, use `ProductTags_Merge`.\n\nProducts carrying the tag report the new name at once.")]
    [McpTool("ProductTags_UpdateFreeTag", "Rename a free tag", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateFreeTag(
        Guid id, [FromBody] UpdateFreeProductTagRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateFreeProductTagCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/merge")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTagCategories)]
    [EndpointSummary("Merge a free tag into another tag.")]
    [EndpointDescription(
        "Every product carrying the free tag `id` carries `targetTagId` instead, and the free tag is deleted. The target may be another free tag, to fold near-duplicates together, or an active tag on an active category — which is how a free tag that caught on is promoted: add the tag to its category with `ProductTagCategories_AddTag`, then merge into it.\n\n**On a category that allows one tag, a product's existing tag there is replaced**, as tagging always does. Each product records the change in its history.")]
    [McpTool("ProductTags_Merge", "Merge a free tag into another tag")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Merge(
        Guid id, [FromBody] MergeProductTagRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToMergeProductTagCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ProductTagCategories)]
    [EndpointSummary("Delete a free tag, taking it off every product that carries it.")]
    [EndpointDescription(
        "**Free tags only**; a tag on a category is deleted with `ProductTagCategories_DeleteTag`. **Unlike that call, this succeeds while products carry the tag: it removes the tag from each of them**, and each records the change. To keep those products filed under another tag, use `ProductTags_Merge` instead. A free tag is also deleted on its own once the last product carrying it loses it.")]
    [McpTool("ProductTags_DeleteFreeTag", "Delete a free tag", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteFreeTag(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteFreeProductTagCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
