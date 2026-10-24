using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeChannels.Dtos;
using Wayd.DemandManagement.Application.IntakeChannels.Queries;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// Intake channels: where requests come from.
/// </summary>
[Route("api/demand-management/intake-channels")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
public class IntakeChannelsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Get the intake channels in the order requesters see them.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeChannelDto>>> GetIntakeChannels([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var channels = await _dispatcher.Send(new GetIntakeChannelsQuery(includeArchived), cancellationToken);
        return Ok(channels);
    }
}
