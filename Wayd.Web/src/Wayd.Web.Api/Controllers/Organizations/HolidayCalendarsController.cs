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
[McpTools(McpToolset.Organization, "wayd-teams")]
public class HolidayCalendarsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.HolidayCalendars)]
    [EndpointSummary("Get the holiday calendars, by name.")]
    [EndpointDescription("Each calendar has its `id` (UUID), `key`, name, description, `holidayCount`, and `isDefault` for the system default calendar, which a team operating model or employee with no calendar of its own takes.")]
    [McpTool("HolidayCalendars_GetList", "List holiday calendars")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HolidayCalendarListDto>>> GetList(CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetHolidayCalendarsQuery(), cancellationToken));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.HolidayCalendars)]
    [EndpointSummary("Get a holiday calendar with its holidays.")]
    [EndpointDescription("Takes the calendar's UUID or integer key. Returns its `holidays` (each with its own `id`, `date` and `name`), `isDefault`, and how many team operating models (`operatingModelCount`) and employees (`employeeCount`) use it.")]
    [McpTool("HolidayCalendars_GetHolidayCalendar", "Get holiday calendar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HolidayCalendarDetailsDto>> Get(string idOrKey, CancellationToken cancellationToken)
    {
        var calendar = await _dispatcher.Send(new GetHolidayCalendarQuery(new IdOrKey(idOrKey)), cancellationToken);
        return calendar is not null ? Ok(calendar) : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.HolidayCalendars)]
    [EndpointSummary("Get a holiday calendar's activity history, newest first: its creation, detail changes, and holidays being added, changed or removed.")]
    [EndpointDescription("Takes the calendar's UUID or integer key. Each entry has a `category`, an `actorKind` with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string, carrying the value before and after a change. Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("HolidayCalendars_GetActivities", "Get holiday calendar activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetHolidayCalendarActivitiesQuery(new IdOrKey(idOrKey), new PageRequest(page, pageSize)), cancellationToken);
        return activities is not null ? Ok(activities) : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.HolidayCalendars)]
    [EndpointSummary("Create a holiday calendar with no holidays.")]
    [EndpointDescription("`name` is unique across calendars. Add its days with HolidayCalendars_AddHoliday, then assign it to a team through its operating model (Teams_SetOperatingModel or Teams_UpdateOperatingModel) or to a person with Employees_ChangeHolidayCalendar. Returns the new calendar's `id` (UUID) and `key`.")]
    [McpTool("HolidayCalendars_Create", "Create holiday calendar", Destructive = false)]
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
    [EndpointSummary("Rename a holiday calendar or change its description.")]
    [EndpointDescription("**This is a whole-record overwrite of both fields: an omitted description is cleared.** Takes the calendar's UUID `id`, which must match the body's `id`. `name` is unique across calendars. Its holidays have their own tools.")]
    [McpTool("HolidayCalendars_Update", "Update holiday calendar")]
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
    [EndpointSummary("Delete a holiday calendar with its holidays.")]
    [EndpointDescription("Takes the calendar's UUID `id`. Refused while it is the system default, or while any team operating model, past or current, or any employee uses it: HolidayCalendars_GetHolidayCalendar gives the counts. Cannot be undone, so confirm with the user before calling.")]
    [McpTool("HolidayCalendars_Delete", "Delete holiday calendar")]
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
    [EndpointSummary("Add a holiday to a calendar.")]
    [EndpointDescription("Takes the calendar's UUID `id`, a `date` (yyyy-MM-dd) and a `name`. A calendar holds at most one holiday per date. Every team and person taking the calendar loses that day from their capacity. Returns the new holiday's id.")]
    [McpTool("HolidayCalendars_AddHoliday", "Add holiday", Destructive = false)]
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
    [EndpointSummary("Move a holiday to another date or rename it.")]
    [EndpointDescription("Takes the calendar's UUID `id` and the holiday's `holidayId` from HolidayCalendars_GetHolidayCalendar. Both `date` and `name` are required and overwrite the holiday's; the new date must not already have a holiday in the calendar.")]
    [McpTool("HolidayCalendars_ChangeHoliday", "Change holiday")]
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
    [EndpointSummary("Remove a holiday from a calendar.")]
    [EndpointDescription("Takes the calendar's UUID `id` and the holiday's `holidayId` from HolidayCalendars_GetHolidayCalendar. The day becomes a working day again for everyone taking the calendar.")]
    [McpTool("HolidayCalendars_RemoveHoliday", "Remove holiday")]
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
