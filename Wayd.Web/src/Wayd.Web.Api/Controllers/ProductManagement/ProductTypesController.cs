using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.ProductTypes.Commands;
using Wayd.ProductManagement.Application.ProductTypes.Dtos;
using Wayd.ProductManagement.Application.ProductTypes.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.ProductTypes;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// The product type catalog: what kinds of node exist, and which of them can carry releases.
/// </summary>
/// <remarks>
/// A type decides what a node may <em>do</em>; tags describe everything else. That is why this list is
/// short and curated while tag axes are open-ended.
/// </remarks>
[Route("api/product-management/product-types")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
[McpTools(McpToolset.Products)]
public class ProductTypesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProductTypes)]
    [OpenApiOperation(
        "List the product types an organization recognises, in the order an administrator arranged them.",
        "**Call this before Products_Create or Products_Retype**, which both need a type UUID.\n\nThe flag that matters is `isReleasable`: it decides whether versions can be cut against products of this type. A product line or a platform is typically not releasable; a service, application or library is. It also gates retyping — a product with versions cannot be moved to a type that is not releasable.\n\nInactive types cannot be assigned to a product, though a product already carrying one keeps it.")]
    [McpTool("ProductTypes_GetProductTypes", "List product types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProductTypeDto>>> GetProductTypes(
        [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var types = await _dispatcher.Send(new GetProductTypesQuery(isActive), cancellationToken);

        return Ok(types);
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ProductTypes)]
    [OpenApiOperation(
        "Define a product type.",
        "**`isReleasable` is the consequential field** — it decides whether versions can be cut against products of this type, and it is what a product line or platform sets to false. Names must be unique. `order` is presentation only and implies no hierarchy.")]
    [McpTool("ProductTypes_Create", "Create a product type", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create(
        [FromBody] CreateProductTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProductTypeCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProductTypes), null, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTypes)]
    [OpenApiOperation(
        "Update a product type.",
        "**This is a whole-record overwrite, and `isReleasable` is required — so renaming a type means resending its current releasability, and sending the wrong value silently changes whether versions can be cut against every product of this type.** Read the type first.\n\nRefused on a seeded system type. Names must stay unique.")]
    [McpTool("ProductTypes_Update", "Update a product type", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(
        Guid id, [FromBody] UpdateProductTypeRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateProductTypeCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/active")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProductTypes)]
    [OpenApiOperation(
        "Take a product type out of use, or put it back.",
        "A deactivated type cannot be assigned to a product, but products already using it keep resolving it — which is why this is deactivation rather than deletion.\n\nUnlike editing, this **is** allowed on a seeded system type: an organization that does not ship libraries should be able to hide that type without the seeder recreating it.")]
    [McpTool("ProductTypes_SetActive", "Activate or deactivate a product type", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SetActive(
        Guid id, [FromBody] SetProductTypeActiveRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToSetProductTypeActiveCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ProductTypes)]
    [OpenApiOperation(
        "Delete a product type.",
        "Seeded system records cannot be modified or deleted, and a record in use cannot be deleted — deactivate it instead, which stops new use without breaking what already refers to it. A type is \"in use\" when any product carries it, so in practice this only removes a type created by mistake and never assigned.")]
    [McpTool("ProductTypes_Delete", "Delete a product type", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProductTypeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
