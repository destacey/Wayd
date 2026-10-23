using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;
using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.ExpenditureCategories;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/expenditure-categories")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Ppm, "wayd-ppm")]
public class ExpenditureCategoriesController(ILogger<ExpenditureCategoriesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<ExpenditureCategoriesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ExpenditureCategories)]
    [EndpointSummary("Get a list of expenditure categories — how project spending is classified (for example Opex or Capex) — in every state.")]
    [EndpointDescription(
        "Each carries its state (Proposed, Active, Archived), whether it is capitalizable and requires depreciation, and its accounting code. To pick a category for a project, ExpenditureCategories_GetOptions is lighter.")]
    [McpTool("ExpenditureCategories_GetExpenditureCategories", "List expenditure categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ExpenditureCategoryListDto>>> GetExpenditureCategories(CancellationToken cancellationToken)
    {
        var expenditures = await _dispatcher.Send(new GetExpenditureCategoriesQuery(), cancellationToken);

        return Ok(expenditures);
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ExpenditureCategories)]
    [EndpointSummary("Get expenditure category details.")]
    [EndpointDescription(
        "Takes the category's numeric id.")]
    [McpTool("ExpenditureCategories_GetExpenditureCategory", "Get expenditure category")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenditureCategoryDetailsDto>> GetExpenditureCategory(int id, CancellationToken cancellationToken)
    {
        var expenditure = await _dispatcher.Send(new GetExpenditureCategoryQuery(id), cancellationToken);

        return expenditure is not null
            ? Ok(expenditure)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ExpenditureCategories)]
    [EndpointSummary("Create an expenditure category.")]
    [EndpointDescription(
        "It starts in Proposed state; a project can be given only an Active category, so activate it with ExpenditureCategories_Activate before use. Returns the new category's numeric id. Confirm with the user before calling.")]
    [McpTool("ExpenditureCategories_Create", "Create expenditure category", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Int))]
    public async Task<ActionResult<int>> Create([FromBody] CreateExpenditureCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateExpenditureCategoryCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetExpenditureCategory), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ExpenditureCategories)]
    [EndpointSummary("Update an expenditure category's name, description, capitalizable and depreciation settings, and accounting code.")]
    [EndpointDescription(
        "This is a whole-record update, not a patch: every field is overwritten from the request body, so an omitted accounting code is cleared and an omitted flag becomes false. Read the category first and echo back every value that should stay the same. Once the category is Active, `isCapitalizable` and `requiresDepreciation` are fixed and must be sent unchanged; an Archived category cannot be updated. The id in the body must match the id path parameter. Projects already using it see the change, so confirm with the user before calling.")]
    [McpTool("ExpenditureCategories_Update", "Update expenditure category")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateExpenditureCategoryRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateExpenditureCategoryCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ExpenditureCategories)]
    [EndpointSummary("Activate a proposed expenditure category so projects can use it.")]
    [EndpointDescription(
        "**Activation fixes its capitalizable and depreciation settings**; the name, description and accounting code stay editable. Only a Proposed category can be activated, and an active one cannot be deleted. Confirm with the user before calling.")]
    [McpTool("ExpenditureCategories_Activate", "Activate expenditure category")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateExpenditureCategoryCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ExpenditureCategories)]
    [EndpointSummary("Archive an active expenditure category, retiring it from new projects.")]
    [EndpointDescription(
        "Projects already using it keep it. An archived category cannot be updated or reactivated. Only an Active category can be archived. Confirm with the user before calling.")]
    [McpTool("ExpenditureCategories_Archive", "Archive expenditure category")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Archive(int id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveExpenditureCategoryCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ExpenditureCategories)]
    [EndpointSummary("Delete an expenditure category.")]
    [EndpointDescription(
        "Permanent, and only for a category still in Proposed state and not used by any project. An active category cannot be deleted; archive it instead (ExpenditureCategories_Archive). Confirm with the user before calling.")]
    [McpTool("ExpenditureCategories_Delete", "Delete expenditure category")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteExpenditureCategoryCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ExpenditureCategories)]
    [EndpointSummary("Get a lightweight list of expenditure category options for lookups.")]
    [EndpointDescription("Use this to resolve the expenditureCategoryId required when creating or updating a project.")]
    [McpTool("ExpenditureCategories_GetOptions", "List expenditure category options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ExpenditureCategoryOptionDto>>> GetExpenditureCategoryOptions([FromQuery] bool? includeArchived, CancellationToken cancellationToken)
    {
        var options = await _dispatcher.Send(new GetExpenditureCategoryOptionsQuery(includeArchived), cancellationToken);

        return Ok(options);
    }
}
