using CsvHelper;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Application.Products.Imports;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.Products;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// The product tree: products, the components beneath them, and the services they are built from.
/// </summary>
/// <remarks>
/// Gated on the module's feature flag, so the whole area 404s until an administrator enables it.
/// <para>
/// Type, parent and status each have their own endpoint rather than being fields on the update. Every
/// one of them carries a rule the aggregate enforces — releases block a retype, ancestry blocks a move,
/// the workflow constrains a status — and folding them into a blanket PUT would hide which rule
/// rejected the change.
/// </para>
/// </remarks>
[Route("api/product-management/products")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
public class ProductsController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Products)]
    [OpenApiOperation(
        "List products from the catalog, ordered by name.",
        "Returns a **flat list, not a tree** — each product carries its parent as a reference, so build the hierarchy client-side.\n\nTwo filter behaviours worth knowing. `parentId` matches **direct children only**, not a whole subtree, and there is no way to ask for root nodes: omitting it returns everything rather than only roots. `tagId` is repeatable and combines as **AND, not OR** — passing a Platform tag and a Compliance tag returns products carrying both.\n\nEach product reports `isReleasable`, flattened from its type, which is what decides whether versions can be cut against it.")]
    [McpTool("Products_GetProducts", "List products")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(
        [FromQuery] Guid? parentId,
        [FromQuery] Guid? productTypeId,
        [FromQuery] StatusCategory[]? statusCategory,
        [FromQuery] Guid[]? tagId,
        CancellationToken cancellationToken)
    {
        var products = await _dispatcher.Send(
            new GetProductsQuery(parentId, productTypeId, statusCategory, tagId), cancellationToken);

        return Ok(products);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Products)]
    [OpenApiOperation(
        "Get one product in full — its type, parent, status, tags, external identifier, and whether its type allows versions to be cut against it.",
        "Accepts the product's UUID or its short key.")]
    [McpTool("Products_GetProduct", "Get product")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProduct(string idOrKey, CancellationToken cancellationToken)
    {
        var product = await _dispatcher.Send(new GetProductQuery(new IdOrKey(idOrKey)), cancellationToken);

        return product is not null
            ? Ok(product)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Products)]
    [OpenApiOperation(
        "Get a product's activity history, newest first: every change recorded on the product — details, type, parent, status, tags and external link — plus a child product moving in or out, listed as a related entry raised on that child.",
        "Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("Products_GetActivities", "Get product activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetProductActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Products)]
    [OpenApiOperation(
        "Get a product's status change history, newest first.",
        "Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past.")]
    [McpTool("Products_GetStatusHistory", "Get product status history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusTransitionDto>>> GetStatusHistory(
        string idOrKey, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new GetProductStatusHistoryQuery(new IdOrKey(idOrKey)), cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpGet("status-options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Products)]
    [OpenApiOperation(
        "Get the statuses a product can be moved to, in the order an administrator laid the lifecycle out rather than alphabetically.",
        "**Call this before Products_ChangeStatus**: that tool needs a status UUID, statuses are per-organization configuration with no fixed list, and any id outside this workflow is refused. The same list serves every product, so one call covers them all.")]
    [McpTool("Products_GetStatusOptions", "Get product status options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StatusNavigationDto>>> GetStatusOptions(
        CancellationToken cancellationToken)
    {
        var statuses = await _dispatcher.Send(new GetProductStatusOptionsQuery(), cancellationToken);

        return Ok(statuses);
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Products)]
    [OpenApiOperation(
        "Add a product to the catalog.",
        "The **type** decides what the node can do — most consequentially whether versions can be cut against it — and must be an active type. Omit the parent to create a root node.\n\nThe external identifier is the node's id in whatever system owns it: a repository, a pipeline, a registry package. Capturing it now makes reconciling against a later automated feed a matching problem rather than a re-authoring one.")]
    [McpTool("Products_Create", "Create a product", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create(
        [FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProductCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProduct), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Products)]
    [OpenApiOperation("Submit a csv file of products to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.", "")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ProductImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportProductRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedProducts = _csvService.ReadCsv<ImportProductRequest>(file.OpenReadStream()).ToList();

            List<SubmittedImportRow<ImportProductDto>> rows = [];
            var validator = new ImportProductRequestValidator();
            for (var i = 0; i < importedProducts.Count; i++)
            {
                var product = importedProducts[i];

                var validationResults = await validator.ValidateAsync(product, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        // The row's own key rather than its name: a catalog legitimately holds the same
                        // name in two places, so naming one would not say which row failed. This is the
                        // same key the run reports outcomes against.
                        error.ErrorMessage =
                            $"{error.ErrorMessage} (Row: {SubmittedImportRow.KeyFor(product.ImportId, i + 1)})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportProductDto>(product.ImportId, product.ToImportProductDto()));
            }

            var result = await _dispatcher.Send(new ImportProductsCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPost("dependencies/import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Products)]
    [OpenApiOperation("Submit a csv file of product dependencies to import. Applied product by product: a product's rows apply together or not at all. Returns the run — 200 once it has finished, 202 while it is still queued or running.", "")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ProductDependencyImportDefinition.ImportKey)]
    public async Task<ActionResult> ImportDependencies([FromForm, CsvRows(typeof(ImportProductDependencyRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, [FromServices] IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        try
        {
            var importedDependencies = _csvService.ReadCsv<ImportProductDependencyRequest>(file.OpenReadStream()).ToList();

            List<SubmittedImportRow<ImportProductDependencyDto>> rows = [];
            var validator = new ImportProductDependencyRequestValidator(dateTimeProvider);
            for (var i = 0; i < importedDependencies.Count; i++)
            {
                var dependency = importedDependencies[i];

                var validationResults = await validator.ValidateAsync(dependency, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage =
                            $"{error.ErrorMessage} (Row: {SubmittedImportRow.KeyFor(dependency.ImportId, i + 1)})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportProductDependencyDto>(dependency.ImportId, dependency.ToImportProductDependencyDto()));
            }

            var result = await _dispatcher.Send(new ImportProductDependenciesCommand(rows, submissionGroupId, validateOnly), cancellationToken);

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
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Update a product's name and description.",
        "**This is a whole-record overwrite of those two fields: an omitted description is cleared.**\n\nOnly the name and description. Type, parent, status, tags and the external link each have their own tool, because each carries a rule this one does not — and keeping the external link out means a rename cannot silently clear it.")]
    [McpTool("Products_Update", "Update a product", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(
        Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateProductDetailsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/external-link")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Set or clear a product's identifier in the system that owns it — a repository, a pipeline, a registry package.",
        "Omitting the value unlinks; there is no separate unlink tool. Free text, max 256 characters, and **not required to be unique**: two products may carry the same identifier.\n\nThis is separate from the ordinary update because it answers a different question — not what the product is called, but which external record it corresponds to — and keeping it apart stops a rename from silently clearing it.")]
    [McpTool("Products_LinkExternally", "Link a product externally", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> LinkExternally(
        Guid id, [FromBody] LinkProductExternallyRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToLinkProductExternallyCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/parent")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Move a product to a different parent, or to the root by omitting the parent.",
        "**Refused if the new parent is the product itself or one of its own descendants** — that would make a cycle. Any type may parent any other; there are no allowed-parent rules.\n\n**Also refused when the move would put two products with an open dependency between them above and below one another** — that relationship is composition, which the tree already records. End the dependency first (`Products_EndDependency`).\n\nThe move is listed in the activity history of the product and of both its old and new parent.")]
    [McpTool("Products_Reparent", "Move a product", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reparent(
        Guid id, [FromBody] ReparentProductRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToReparentProductCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/type")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Change a product's type.",
        "**Refused if the product has versions and the new type is not releasable** — the versions already cut against it would be left hanging off a node that cannot carry them. The target type must be active, unless it is the type the product already has.\n\nNote this is gated on *versions*, not releases: releasability asks whether an artifact can be cut against a node, and a release is an announcement that may sit under any node.")]
    [McpTool("Products_Retype", "Change a product's type", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Retype(
        Guid id, [FromBody] RetypeProductRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToRetypeProductCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/status")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Move a product to a different status.",
        "**Call Products_GetStatusOptions first** — this needs a status UUID, and statuses are per-organization configuration rather than a fixed set. A status belonging to a different workflow is refused.\n\nAny status in the product workflow is reachable from any other; there is no transition graph. The status name is frozen onto the history at the moment of the change, so renaming a status later does not rewrite what past entries read as.")]
    [McpTool("Products_ChangeStatus", "Change a product's status", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeStatus(
        Guid id, [FromBody] ChangeProductStatusRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToChangeProductStatusCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/tags/{tagId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Apply a tag to a product.",
        "Tags live in categories — axes such as Platform or Compliance — and a category decides whether a product may carry more than one of its tags.\n\n**On a single-value axis this silently replaces the existing tag rather than refusing.** The call succeeds, and the tag the product previously carried on that axis is gone. Read the product first if that matters. On a multi-value axis the tag joins the others.\n\nBoth the tag and its category must be active. Applying a tag the product already carries succeeds and changes nothing.")]
    [McpTool("Products_Tag", "Tag a product")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Tag(Guid id, Guid tagId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new TagProductCommand(id, tagId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/tags/{tagId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation("Remove a tag from a product.", "Succeeds whether or not the product carried it, and an inactive tag can still be removed.")]
    [McpTool("Products_Untag", "Remove a tag from a product", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Untag(Guid id, Guid tagId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UntagProductCommand(id, tagId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/dependencies")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Products)]
    [OpenApiOperation(
        "Get what a product depends on (`dependsOn`) and what depends on it (`usedBy`).",
        "Accepts the product's UUID or its short key.\n\n**Rolled up across everything beneath the product.** A link from one of its children to an outside product appears under `dependsOn`, and a link with both ends inside the product's own subtree appears in **neither** list — from outside, that is the product depending on itself. So reading a product line answers \"what does this line rely on from elsewhere\", and reading a leaf service answers it for that service alone.\n\nEach entry carries both ends whichever list it is in — `product` (the one with the dependency) and `dependsOnProduct` — so a rolled-up row says which descendant it starts or lands on. `productPath` and `dependsOnProductPath` give each end's full ancestry, root first, down to its parent. Also `strength` (1 Hard: stops working without it; 2 Soft: degrades but keeps working), `description`, `startsOn`, and `endsOn` (null while it still holds).\n\n`interactionStyles` lists how the product reaches the one it depends on — `Synchronous`, `Asynchronous`, or both. It refines `strength`: a Hard **synchronous** dependency caps the consumer's availability at the provider's, while a Hard **asynchronous** one turns the provider's downtime into a backlog the consumer works through afterwards. **`null` means nobody has recorded them, not that there are none** — do not read it as evidence either way.\n\nEnded dependencies are left out unless `includeEnded` is true. An empty answer means no dependency has been recorded, not that none exists — they are entered by hand or by import.")]
    [McpTool("Products_GetDependencies", "Get product dependencies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDependenciesDto>> GetDependencies(
        string idOrKey, [FromQuery] bool includeEnded = false, CancellationToken cancellationToken = default)
    {
        var dependencies = await _dispatcher.Send(
            new GetProductDependenciesQuery(new IdOrKey(idOrKey), includeEnded), cancellationToken);

        return dependencies is not null
            ? Ok(dependencies)
            : NotFound();
    }

    [HttpPost("{id}/dependencies")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Record that a product depends on another.",
        "Returns the new dependency's id.\n\nRecord the **most specific product known** — the service that makes the call, not the platform it belongs to — since the read side rolls links up to every ancestor anyway. A product cannot depend on itself, nor on anything above or below it in the tree: that is composition, which the tree already records.\n\n**Strength has no default and must be chosen deliberately**: 1 Hard if the product stops working without it, 2 Soft if it degrades or loses a feature but keeps working. Attributing a provider's downtime to its consumers reads this, so guessing either way misstates impact — ask if the person has not said.\n\n`interactionStyles` says how the product reaches it — `Synchronous`, `Asynchronous`, or both, since a pair commonly calls for what it needs now and subscribes for what it needs eventually. Unlike strength it has no requirement to be set, but leaving it out records nothing rather than recording that there are none, so supply it when the person has said.\n\nA product holds at most one open dependency on another product, and a later one on the same pair cannot overlap an earlier one. `startsOn` defaults to today, may be backdated, and cannot be in the future.")]
    [McpTool("Products_AddDependency", "Add a product dependency", Destructive = false)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AddDependency(
        Guid id, [FromBody] AddProductDependencyRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddProductDependencyCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetDependencies), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/dependencies/{dependencyId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Reword what a product's dependency is for, and record its interaction styles where none have been recorded.",
        "Terms and dates each have their own tool. Allowed on an ended dependency, since both describe the dependency rather than asserting anything about when it held.\n\n**An omitted description is cleared, but omitted `interactionStyles` are left alone.** The asymmetry is deliberate: a description is prose somebody may want emptied, whereas styles are read when downtime is attributed, and omitting the field would otherwise erase them.\n\n**This tool only fills a blank.** Changing styles already recorded is refused — that is a change of terms, which has to be dated, so use `Products_ChangeDependencyTerms`.")]
    [McpTool("Products_UpdateDependency", "Update a product dependency", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateDependency(
        Guid id, Guid dependencyId, [FromBody] UpdateProductDependencyRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new UpdateProductDependencyCommand(id, dependencyId, request.Description, request.InteractionStyles), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/dependencies/{dependencyId}/end")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Record that a product stopped depending on another.",
        "**The dependency is kept** and still counts for the period it held — this is the right tool when a dependency was true and no longer is. Use `Products_RemoveDependency` only for one that was never true.\n\n`endsOn` is the last day it held: defaults to today, may be the day it started, and cannot be in the future.")]
    [McpTool("Products_EndDependency", "End a product dependency")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> EndDependency(
        Guid id, Guid dependencyId, [FromBody] EndProductDependencyRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new EndProductDependencyCommand(id, dependencyId, request.EndsOn), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/dependencies/{dependencyId}/terms")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "Change the terms a product's dependency holds on — its strength, how the product reaches it, or both.",
        "**This ends the current dependency and records a new one** on the new terms, so the history keeps when each set of terms held — and the response is the id of the dependency **now open**, which replaces the one you passed. Unchanged when the terms already match.\n\n**`strength` is required and is the whole record's strength, not a delta.** Passing only `interactionStyles` still needs the current strength repeated, or you will silently change it.\n\n**Omitted `interactionStyles` carry the recorded ones onto the new dependency** rather than clearing them.\n\nRecording styles on a dependency that had none is the exception: nothing about the dependency changed, somebody finally wrote down how it had always worked, so it fills them in place, returns the **same** id, and `changedOn` is ignored. Dating that would split the period on a day nothing happened.\n\n`changedOn` is the first day the new terms hold; the current dependency ends the day before. Defaults to today, must be after the day the dependency started, and cannot be in the future.")]
    [McpTool("Products_ChangeDependencyTerms", "Change a product dependency terms", Idempotent = false)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> ChangeDependencyTerms(
        Guid id, Guid dependencyId, [FromBody] ChangeProductDependencyTermsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            request.ToChangeProductDependencyTermsCommand(id, dependencyId), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/dependencies/{dependencyId}/remove")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Products)]
    [OpenApiOperation(
        "**Delete** a dependency that was recorded by mistake.",
        "Requires a reason saying why it was never true. Not for a dependency that stopped — `Products_EndDependency` keeps the history of when it held, and this erases it.")]
    [McpTool("Products_RemoveDependency", "Remove a product dependency")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> RemoveDependency(
        Guid id, Guid dependencyId, [FromBody] RemoveProductDependencyRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new RemoveProductDependencyCommand(id, dependencyId, request.Reason), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Products)]
    [OpenApiOperation(
        "Permanently delete a product.",
        "**This is a hard delete, not a retirement.** Consider changing the status instead if the product merely stopped being current. It takes its status history with it; its activity history is kept.\n\nRefused while anything depends on it, each with its own reason: it has **child products** (move or remove them first), it has **versions**, it appears in a **release package manifest**, or it is named on **either end of a product dependency**. The manifest check is separate from versions because a carried-forward manifest line often names a product with no version row at all. The dependency check counts **ended** dependencies too — deleting the product would erase the record of what relied on it.\n\nTag assignments are removed with the product. Status does not block deletion.")]
    [McpTool("Products_Delete", "Delete a product", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveProductCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
