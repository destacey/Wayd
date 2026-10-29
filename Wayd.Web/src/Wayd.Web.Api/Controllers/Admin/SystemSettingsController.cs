using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings.ProductTags.Dtos;
using Wayd.Common.Application.SystemSettings.ProductTags.Queries;
using Wayd.Common.Application.SystemSettings.Scheduling.Dtos;
using Wayd.Common.Application.SystemSettings.Scheduling.Queries;
using Wayd.Common.Application.SystemSettings.SprintMetrics.Dtos;
using Wayd.Common.Application.SystemSettings.SprintMetrics.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Admin.SystemSettings;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/admin/settings")]
[ApiVersionNeutral]
[ApiController]
public class SystemSettingsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet("scheduling")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.SystemSettings)]
    [EndpointSummary("Get the scheduling settings.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchedulingSettingsDto>> GetSchedulingSettings(CancellationToken cancellationToken)
    {
        var settings = await _dispatcher.Send(new GetSchedulingSettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("scheduling")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.SystemSettings)]
    [EndpointSummary("Update the scheduling settings.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateSchedulingSettings([FromBody] UpdateSchedulingSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateSchedulingSettingsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("scheduling/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.SystemSettings)]
    [EndpointSummary("Get the scheduling settings' change history.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetSchedulingSettingsActivities([FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetSchedulingSettingsActivitiesQuery(new PageRequest(page, pageSize)), cancellationToken);
        return Ok(activities);
    }

    [HttpGet("sprint-metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.SystemSettings)]
    [EndpointSummary("Get the sprint metrics settings.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintMetricsSettingsDto>> GetSprintMetricsSettings(CancellationToken cancellationToken)
    {
        var settings = await _dispatcher.Send(new GetSprintMetricsSettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("sprint-metrics")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.SystemSettings)]
    [EndpointSummary("Update the sprint metrics settings.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateSprintMetricsSettings([FromBody] UpdateSprintMetricsSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateSprintMetricsSettingsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("sprint-metrics/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.SystemSettings)]
    [EndpointSummary("Get the sprint metrics settings' change history.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetSprintMetricsSettingsActivities([FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetSprintMetricsSettingsActivitiesQuery(new PageRequest(page, pageSize)), cancellationToken);
        return Ok(activities);
    }

    [HttpGet("product-tags")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.SystemSettings)]
    [EndpointSummary("Get the product tag settings.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductTagSettingsDto>> GetProductTagSettings(CancellationToken cancellationToken)
    {
        var settings = await _dispatcher.Send(new GetProductTagSettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("product-tags")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.SystemSettings)]
    [EndpointSummary("Update the product tag settings.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateProductTagSettings([FromBody] UpdateProductTagSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateProductTagSettingsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("product-tags/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.SystemSettings)]
    [EndpointSummary("Get the product tag settings' change history.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetProductTagSettingsActivities([FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetProductTagSettingsActivitiesQuery(new PageRequest(page, pageSize)), cancellationToken);
        return Ok(activities);
    }
}
