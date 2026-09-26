using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings.Scheduling.Dtos;
using Wayd.Common.Application.SystemSettings.Scheduling.Queries;
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
    [OpenApiOperation("Get the scheduling settings.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchedulingSettingsDto>> GetSchedulingSettings(CancellationToken cancellationToken)
    {
        var settings = await _dispatcher.Send(new GetSchedulingSettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("scheduling")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.SystemSettings)]
    [OpenApiOperation("Update the scheduling settings.", "")]
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
    [OpenApiOperation("Get the scheduling settings' change history.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetSchedulingSettingsActivities([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetSchedulingSettingsActivitiesQuery(page, pageSize), cancellationToken);
        return Ok(activities);
    }
}
