using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;
using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.ExpenditureCategories;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/expenditure-categories")]
[ApiVersionNeutral]
[ApiController]
public class ExpenditureCategoriesController(ILogger<ExpenditureCategoriesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<ExpenditureCategoriesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ExpenditureCategories)]
    [OpenApiOperation("Get a list of expenditure categories.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ExpenditureCategoryListDto>>> GetExpenditureCategories(CancellationToken cancellationToken)
    {
        var expenditures = await _dispatcher.Send(new GetExpenditureCategoriesQuery(), cancellationToken);

        return Ok(expenditures);
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ExpenditureCategories)]
    [OpenApiOperation("Get expenditure category details.", "")]
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
    [OpenApiOperation("Create an expenditure category.", "")]
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
    [OpenApiOperation("Update an expenditure category.", "")]
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
    [OpenApiOperation("Activate an expenditure category.", "")]
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
    [OpenApiOperation("Archive an expenditure category.", "")]
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
    [OpenApiOperation("Delete an expenditure category.", "")]
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
    [OpenApiOperation("Get a list of expenditure categories options.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ExpenditureCategoryOptionDto>>> GetExpenditureCategoryOptions([FromQuery] bool? includeArchived, CancellationToken cancellationToken)
    {
        var options = await _dispatcher.Send(new GetExpenditureCategoryOptionsQuery(includeArchived), cancellationToken);

        return Ok(options);
    }
}
