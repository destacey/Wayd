using Wayd.Common.Application.HealthChecks.Dtos;
using Wayd.Common.Application.HealthChecks.Queries;

namespace Wayd.Web.Api.Controllers.HealthChecks;

[Route("api/health-checks")]
[ApiVersionNeutral]
[ApiController]
public class HealthChecksController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public HealthChecksController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.HealthChecks)]
    [EndpointSummary("Get the list of health check statuses.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HealthStatusDto>>> GetStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetHealthStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }
}
