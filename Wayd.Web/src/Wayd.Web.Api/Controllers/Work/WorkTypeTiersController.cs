using Wayd.Work.Application.WorkTypeTiers.Dtos;
using Wayd.Work.Application.WorkTypeTiers.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-type-tiers")]
[ApiVersionNeutral]
[ApiController]
public class WorkTypeTiersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WorkTypeTiersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkTypeTiers)]
    [OpenApiOperation("Get a list of all work type tiers.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkTypeTierDto>>> GetList(CancellationToken cancellationToken)
    {
        var tiers = await _dispatcher.Send(new GetWorkTypeTiersQuery(), cancellationToken);
        return Ok(tiers.OrderBy(c => c.Order));
    }
}
