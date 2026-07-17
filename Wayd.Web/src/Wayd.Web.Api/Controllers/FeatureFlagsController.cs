using Wayd.Common.Application.FeatureManagement.Dtos;
using Wayd.Common.Application.FeatureManagement.Queries;

namespace Wayd.Web.Api.Controllers;

[Route("api/feature-flags")]
[ApiVersionNeutral]
[ApiController]
public class FeatureFlagsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [OpenApiOperation("Get all enabled feature flags for the current user.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ClientFeatureFlagDto>>> GetEnabledFeatureFlags(CancellationToken cancellationToken)
    {
        var flags = await _dispatcher.Send(new GetClientFeatureFlagsQuery(), cancellationToken);
        return Ok(flags);
    }
}
