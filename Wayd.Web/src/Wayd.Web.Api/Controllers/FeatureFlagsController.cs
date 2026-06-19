using Wayd.Common.Application.FeatureManagement.Dtos;
using Wayd.Common.Application.FeatureManagement.Queries;

namespace Wayd.Web.Api.Controllers;

[Route("api/feature-flags")]
[ApiVersionNeutral]
[ApiController]
public class FeatureFlagsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpGet]
    [OpenApiOperation("Get all enabled feature flags for the current user.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ClientFeatureFlagDto>>> GetEnabledFeatureFlags(CancellationToken cancellationToken)
    {
        var flags = await _sender.Send(new GetClientFeatureFlagsQuery(), cancellationToken);
        return Ok(flags);
    }
}
