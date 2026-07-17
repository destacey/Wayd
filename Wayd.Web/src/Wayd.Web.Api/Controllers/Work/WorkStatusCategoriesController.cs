using Wayd.Work.Application.WorkStatusCategories.Dtos;
using Wayd.Work.Application.WorkStatusCategories.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-status-categories")]
[ApiVersionNeutral]
[ApiController]
public class WorkStatusCategoriesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WorkStatusCategoriesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkStatusCategories)]
    [OpenApiOperation("Get a list of all work status categories.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkStatusCategoryListDto>>> GetList(CancellationToken cancellationToken)
    {
        var categories = await _dispatcher.Send(new GetWorkStatusCategoriesQuery(), cancellationToken);
        return Ok(categories.OrderBy(c => c.Order));
    }
}
