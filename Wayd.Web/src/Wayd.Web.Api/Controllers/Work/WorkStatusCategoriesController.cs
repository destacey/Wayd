using Wayd.Work.Application.WorkStatusCategories.Dtos;
using Wayd.Work.Application.WorkStatusCategories.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-status-categories")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Work, "wayd-work")]
public class WorkStatusCategoriesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WorkStatusCategoriesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkStatusCategories)]
    [EndpointSummary("Get a list of all work status categories.")]
    [EndpointDescription("The fixed categories every workflow places its statuses in, so work can be compared across workflows: Proposed (not started), Active (in progress), Done (completed) and Removed (left without being completed), in that order.")]
    [McpTool("WorkStatusCategories_GetWorkStatusCategories", "List work status categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkStatusCategoryListDto>>> GetList(CancellationToken cancellationToken)
    {
        var categories = await _dispatcher.Send(new GetWorkStatusCategoriesQuery(), cancellationToken);
        return Ok(categories.OrderBy(c => c.Order));
    }
}
