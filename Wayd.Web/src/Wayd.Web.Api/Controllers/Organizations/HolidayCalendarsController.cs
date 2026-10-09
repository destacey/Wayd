using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Organization.Application.HolidayCalendars.Commands;
using Wayd.Organization.Application.HolidayCalendars.Dtos;
using Wayd.Organization.Application.HolidayCalendars.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Organizations.HolidayCalendars;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/holiday-calendars")]
[ApiVersionNeutral]
[ApiController]
public class HolidayCalendarsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Get the holiday calendars, by name.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HolidayCalendarListDto>>> GetList(CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetHolidayCalendarsQuery(), cancellationToken));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Get a holiday calendar with its holidays.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HolidayCalendarDetailsDto>> Get(string idOrKey, CancellationToken cancellationToken)
    {
        var calendar = await _dispatcher.Send(new GetHolidayCalendarQuery(new IdOrKey(idOrKey)), cancellationToken);
        return calendar is not null ? Ok(calendar) : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Get a holiday calendar's activity history, newest first: its creation, detail changes, and holidays being added, changed or removed.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetHolidayCalendarActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);
        return activities is not null ? Ok(activities) : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Create a holiday calendar with no holidays.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateHolidayCalendarRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateHolidayCalendarCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { idOrKey = result.Value.Key.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Rename a holiday calendar or change its description.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateHolidayCalendarRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateHolidayCalendarCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:guid}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Delete a holiday calendar with its holidays.", "Refused while a team operating model uses the calendar or it is the system default.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteHolidayCalendarCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/holidays")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Add a holiday to a calendar.", "A calendar holds at most one holiday per date.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult> AddHoliday(Guid id, [FromBody] HolidayRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new AddHolidayCommand(id, request.Date, request.Name), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}/holidays/{holidayId:guid}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Move a holiday to another date or rename it.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangeHoliday(Guid id, Guid holidayId, [FromBody] HolidayRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ChangeHolidayCommand(id, holidayId, request.Date, request.Name), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:guid}/holidays/{holidayId:guid}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.HolidayCalendars)]
    [OpenApiOperation("Remove a holiday from a calendar.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveHoliday(Guid id, Guid holidayId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveHolidayCommand(id, holidayId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
