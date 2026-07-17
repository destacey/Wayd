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
    [OpenApiOperation("Get a list of all feature flags.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<FeatureFlagListDto>>> FeatureFlags(CancellationToken cancellationToken, [FromQuery] bool includeArchived = false)
    {
        var flags = await _dispatcher.Send(new GetFeatureFlagsQuery(includeArchived), cancellationToken);
        return Ok(flags);
    }

    [HttpGet("{id:int}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.FeatureFlags)]
    [OpenApiOperation("Get feature flag details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagDto>> FeatureFlag(int id, CancellationToken cancellationToken)
    {
        var flag = await _dispatcher.Send(new GetFeatureFlagQuery(id), cancellationToken);
        return flag is not null
            ? Ok(flag)
            : NotFound();
    }

    [HttpPut("{id:int}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.FeatureFlags)]
    [OpenApiOperation("Update a feature flag.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateFeatureFlagRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.Id), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateFeatureFlagCommand(), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:int}/toggle")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.FeatureFlags)]
    [OpenApiOperation("Toggle a feature flag on or off.", "")]
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

    [HttpPut("{id:int}/archive")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.FeatureFlags)]
    [OpenApiOperation("Archive a feature flag.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Archive(int id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveFeatureFlagCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
