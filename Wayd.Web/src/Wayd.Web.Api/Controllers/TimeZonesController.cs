using Microsoft.AspNetCore.Authorization;
using Wayd.Common.Application.TimeZones.Dtos;
using Wayd.Common.Application.TimeZones.Queries;

namespace Wayd.Web.Api.Controllers;

[Route("api/time-zones")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
public class TimeZonesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [OpenApiOperation("Get the IANA time zones that can be chosen.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TimeZoneDto>>> GetTimeZones(CancellationToken cancellationToken)
    {
        var zones = await _dispatcher.Send(new GetTimeZonesQuery(), cancellationToken);
        return Ok(zones);
    }
}
