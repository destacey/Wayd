using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.FeatureManagement.Dtos;
using Wayd.Common.Application.FeatureManagement.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Admin;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/admin/feature-flags")]
[ApiVersionNeutral]
[ApiController]
public class FeatureFlagsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.FeatureFlags)]
    [EndpointSummary("Get the feature flags defined in code.")]
    [EndpointDescription("Each flag's name, description and module come from its definition in code; only whether it is enabled can change. Flags no longer defined in code are archived and left out.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeatureFlagDto>>> FeatureFlags(CancellationToken cancellationToken)
    {
        var flags = await _dispatcher.Send(new GetFeatureFlagsQuery(), cancellationToken);
        return Ok(flags);
    }

    [HttpGet("modules")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.FeatureFlags)]
    [EndpointSummary("Get the modules feature flags are grouped under.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeatureFlagModuleDto>>> Modules(CancellationToken cancellationToken)
    {
        var modules = await _dispatcher.Send(new GetFeatureFlagModulesQuery(), cancellationToken);
        return Ok(modules);
    }

    [HttpPut("{id:int}/toggle")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.FeatureFlags)]
    [EndpointSummary("Toggle a feature flag on or off.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Toggle(int id, [FromBody] ToggleFeatureFlagRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.Id), HttpContext));

        var result = await _dispatcher.Send(new ToggleFeatureFlagCommand(id, request.IsEnabled), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
