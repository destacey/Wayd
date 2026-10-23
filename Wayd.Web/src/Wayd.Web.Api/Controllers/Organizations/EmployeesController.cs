using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Employees.Imports;
using Wayd.Common.Application.Employees.Queries;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Organizations.Employees;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/employees")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Organization, "wayd-employees")]
public class EmployeesController(
    ILogger<EmployeesController> logger,
    IDispatcher dispatcher,
    ICsvService csvService) : ControllerBase
{
    private readonly ILogger<EmployeesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [EndpointSummary("Get a list of all employees.")]
    [EndpointDescription(
        "Returns active employees only unless `includeInactive` is true, sorted by last name. An employee is the person record that role, assignee and `employeeId` fields take, not a sign-in account. Each carries its UUID `id` (what those fields and the other employee tools take), integer `key`, employee number, email, job title, department and `manager`. Call it once and filter the result to resolve names to ids.")]
    [McpTool("Employees_GetList", "List employees")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EmployeeListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var employees = await _dispatcher.Send(new GetEmployeesQuery(includeInactive), cancellationToken);
        return Ok(employees.OrderBy(e => e.LastName));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [EndpointSummary("Get employee details using the Id or key.")]
    [EndpointDescription(
        "Takes the employee's UUID `id` or integer `key`; the other employee tools take the UUID only. Returns the name parts, every email address the person is known by, employee number, hire date, job details, `manager` and `isActive`, active or not.")]
    [McpTool("Employees_GetEmployee", "Get employee")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailsDto>> GetEmployee(string idOrKey, CancellationToken cancellationToken)
    {
        var employee = await _dispatcher.Send(new GetEmployeeQuery(idOrKey), cancellationToken);

        return employee is not null
            ? Ok(employee)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Employees)]
    [EndpointSummary("Create an employee.")]
    [EndpointDescription(
        "Adds an active person record by hand, for someone no people connector syncs. Creates no sign-in account. `employeeNumber` must be unique across employees. `managerId` is another employee's UUID; one that matches no employee is refused. Returns the new employee's id and key.")]
    [McpTool("Employees_Create", "Create employee", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateEmployeeCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetEmployee), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Employees)]
    [EndpointSummary("Import employees from a csv file. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(EmployeeImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportEmployeeRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedEmployees = _csvService.ReadCsv<ImportEmployeeRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportEmployeeDto>> rows = [];
            var validator = new ImportEmployeeRequestValidator();
            foreach (var employee in importedEmployees)
            {
                var validationResults = await validator.ValidateAsync(employee, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Employee Number: {employee.EmployeeNumber})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportEmployeeDto>(
                    employee.ImportId, employee.ToImportEmployeeDto()));
            }

            var result = await _dispatcher.Send(new ImportEmployeesCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Employees)]
    [EndpointSummary("Update an employee.")]
    [EndpointDescription(
        "This is a whole-record overwrite: every field is set from the request body, so an omitted field is cleared — including `managerId`, which removes the manager. Read the employee first and send back every value that should stay. The `id` in the body must match the path. `employeeNumber` must stay unique, and a `managerId` that matches no employee is refused. Does not change whether the employee is active. An employee synced by a people connector is overwritten from the source system at its next sync.")]
    [McpTool("Employees_Update", "Update employee")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateEmployeeCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:guid}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Employees)]
    [EndpointSummary("Delete an employee.")]
    [EndpointDescription(
        "Permanently deletes the employee record by UUID; returns 404 when no employee has the id. A people connector recreates an employee it still syncs at its next sync; to remove a synced record that should not exist, use Employees_RemoveInvalid instead.")]
    [McpTool("Employees_Delete", "Delete employee")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await _dispatcher.Send(new EmployeeExistsQuery(id), cancellationToken))
            return NotFound();

        var result = await _dispatcher.Send(new DeleteEmployeeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/direct-reports")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [EndpointSummary("Get a list of direct reports for an employee.")]
    [EndpointDescription(
        "Takes the manager's UUID `id` and returns the active employees whose manager they are, sorted by last name: one level only, not the whole reporting tree. An unknown id returns an empty list.")]
    [McpTool("Employees_GetDirectReports", "List direct reports")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EmployeeListDto>>> GetDirectReports(Guid id, CancellationToken cancellationToken)
    {
        var directReports = await _dispatcher.Send(new GetDirectReportsQuery(id), cancellationToken);
        return Ok(directReports.OrderBy(e => e.LastName));
    }


    [HttpPost("{id}/remove-invalid")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Employees)]
    [EndpointSummary("Remove invalid employee record from employee list.")]
    [EndpointDescription(
        "For a record a people connector synced that is not a real employee, such as a shared mailbox or service account. Clears the manager of its direct reports, deletes the employee, and blocks its employee number so the connector never brings it back; no tool undoes the block.")]
    [McpTool("Employees_RemoveInvalid", "Remove invalid employee")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveInvalid(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveInvalidEmployeeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/team-memberships")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [EndpointSummary("Get team memberships for an employee.")]
    [EndpointDescription(
        "Takes the employee's UUID `id` and returns one entry per team or team of teams they belong to, sorted by team name, each with the team and the member roles they hold on it. Membership is not allocation: Employees_GetAllocations says how much of their time each team has. Returns 404 when no employee has the id.")]
    [McpTool("Employees_GetTeamMemberships", "List employee team memberships")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetTeamMemberships(Guid id, CancellationToken cancellationToken)
    {
        var memberships = await _dispatcher.Send(new GetEmployeeTeamMembershipsQuery(id), cancellationToken);

        return memberships is not null
            ? Ok(memberships)
            : NotFound();
    }

    [HttpGet("{id:guid}/allocations")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [EndpointSummary("Get how an employee's time is allocated to teams over time.")]
    [EndpointDescription(
        "Returns every allocation across all teams in date order: the team, the role the time is spent in when one was chosen, the date range (`end` is null when it has no end) and the `percent` of the employee's time. An employee's allocations never add up to more than 100% on any day. `canManage` says whether the caller may change that team's allocations. Requires the team-capacity-management feature flag; returns 404 when it is off or no employee has the id.")]
    [McpTool("Employees_GetAllocations", "Get employee allocations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeAllocationDto>>> GetAllocations(Guid id, CancellationToken cancellationToken)
    {
        var allocations = await _dispatcher.Send(new GetEmployeeAllocationsQuery(id), cancellationToken);

        return allocations is not null
            ? Ok(allocations)
            : NotFound();
    }

    [HttpPut("{id:guid}/allocations")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Set the share of an employee's time one or more teams have over date ranges.")]
    [EndpointDescription(
        "Each change replaces what its team and role have over its date range and leaves other days as they were, so a change inside a longer allocation splits it; a `percent` of 0 ends the allocation for the range. The changes apply together or not at all, and fail if any day would then exceed 100% across all teams. The caller must be a member of every team named, or of its team of teams, unless they hold the Administer Teams permission. Requires the team-capacity-management feature flag; returns 404 when it is off or no employee has the id.")]
    [McpTool("Employees_ChangeAllocations", "Change employee allocations")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangeAllocations(Guid id, [FromBody] ChangeEmployeeAllocationsRequest request, CancellationToken cancellationToken)
    {
        if (!await _dispatcher.Send(new EmployeeExistsQuery(id), cancellationToken))
            return NotFound();

        var result = await _dispatcher.Send(request.ToChangeEmployeeAllocationsCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/allocations/move")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Move a share of an employee's time from one team and role to another over a date range.")]
    [EndpointDescription(
        "Lowers the source and raises the target by `percent` on every day of the range as one change, so a fully allocated employee can be moved between teams. Fails if the source has less than `percent` on any day of the range. The caller must be a member of both teams, or of their teams of teams, unless they hold the Administer Teams permission. Requires the team-capacity-management feature flag; returns 404 when it is off or no employee has the id.")]
    [McpTool("Employees_MoveAllocation", "Move employee allocation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MoveAllocation(Guid id, [FromBody] MoveEmployeeAllocationRequest request, CancellationToken cancellationToken)
    {
        if (!await _dispatcher.Send(new EmployeeExistsQuery(id), cancellationToken))
            return NotFound();

        var result = await _dispatcher.Send(request.ToMoveEmployeeAllocationCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("time-away-reasons")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [EndpointSummary("Get the reasons time away can be recorded for.")]
    [EndpointDescription(
        "Each reason's `code` is what a stretch of time away carries and what the time away requests accept. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("Employees_GetTimeAwayReasons", "List time away reasons")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TimeAwayReasonDto>>> GetTimeAwayReasons(CancellationToken cancellationToken)
    {
        var reasons = await _dispatcher.Send(new GetTimeAwayReasonsQuery(), cancellationToken);
        return Ok(reasons.OrderBy(r => r.Order));
    }

    [HttpGet("{id:guid}/time-away")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [EndpointSummary("Get when an employee is away and the holiday calendar they take.")]
    [EndpointDescription(
        "Returns each stretch of time away in date order with the share of each day it takes (`percentAway`), and the holiday calendar the employee follows when it is not the system default. Time away lowers the employee's availability to every team they are allocated to and never counts toward their 100% allocation. `canManage` says whether the caller may record this employee's time away: the employee, anyone they report up to, and holders of the Administer Employee Time Away permission. Each stretch's `reason` is null unless `canManage` is true. Requires the team-capacity-management feature flag; returns 404 when it is off or no employee has the id.")]
    [McpTool("Employees_GetTimeAway", "Get employee time away")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeTimeAwayDto>> GetTimeAway(Guid id, CancellationToken cancellationToken)
    {
        var timeAway = await _dispatcher.Send(new GetEmployeeTimeAwayQuery(id), cancellationToken);

        return timeAway is not null
            ? Ok(timeAway)
            : NotFound();
    }

    [HttpPost("{id:guid}/time-away")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [Authorize]
    [EndpointSummary("Record an employee as away for a stretch of days.")]
    [EndpointDescription(
        "Records the employee as away from every team they are allocated to for `percentAway` of each day from `start` to `end`. Stretches may overlap, but a day cannot add up to more than a whole day away. Needs no permission: allowed for the employee, anyone they report up to, and holders of the Administer Employee Time Away permission, and the caller must be linked to an employee. Returns the id of the time away. Requires the team-capacity-management feature flag; returns 404 when it is off. No employee having the id returns 404 to a caller allowed to manage any employee's time away and the same refusal as any other employee to everyone else.")]
    [McpTool("Employees_RecordTimeAway", "Record employee time away", Destructive = false)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> RecordTimeAway(Guid id, [FromBody] EmployeeTimeAwayRequest request, CancellationToken cancellationToken)
    {
        if (await _dispatcher.Send(new IsTimeAwayEmployeeMissingQuery(id), cancellationToken))
            return NotFound();

        var result = await _dispatcher.Send(request.ToRecordEmployeeTimeAwayCommand(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}/time-away/{timeAwayId:guid}")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [Authorize]
    [EndpointSummary("Change a stretch of an employee's time away.")]
    [EndpointDescription(
        "Changes its days, the share of each day it takes, or its reason. This is a whole-record overwrite: `start`, `end`, `percentAway` and `reason` are all set from the body, so read the stretch with Employees_GetTimeAway first and send back the values that should stay. Takes the employee's UUID and the stretch's `id`. Allowed for the same people as recording it. Requires the team-capacity-management feature flag; returns 404 when it is off. No employee having the id returns 404 to a caller allowed to manage any employee's time away and the same refusal as any other employee to everyone else.")]
    [McpTool("Employees_ChangeTimeAway", "Change employee time away")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangeTimeAway(Guid id, Guid timeAwayId, [FromBody] EmployeeTimeAwayRequest request, CancellationToken cancellationToken)
    {
        if (await _dispatcher.Send(new IsTimeAwayEmployeeMissingQuery(id), cancellationToken))
            return NotFound();

        var result = await _dispatcher.Send(request.ToChangeEmployeeTimeAwayCommand(id, timeAwayId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:guid}/time-away/{timeAwayId:guid}")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [Authorize]
    [EndpointSummary("Remove a stretch of an employee's time away.")]
    [EndpointDescription(
        "The employee counts as available on those days again. Allowed for the same people as recording it, whether or not the employee is still active. Requires the team-capacity-management feature flag; returns 404 when it is off. No employee having the id returns 404 to a caller allowed to manage any employee's time away and the same refusal as any other employee to everyone else.")]
    [McpTool("Employees_RemoveTimeAway", "Remove employee time away")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveTimeAway(Guid id, Guid timeAwayId, CancellationToken cancellationToken)
    {
        if (await _dispatcher.Send(new IsTimeAwayEmployeeMissingQuery(id), cancellationToken))
            return NotFound();

        var result = await _dispatcher.Send(new RemoveEmployeeTimeAwayCommand(id, timeAwayId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}/holiday-calendar")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Employees)]
    [EndpointSummary("Set the holiday calendar an employee follows.")]
    [EndpointDescription(
        "Calendars are shared: assign the one for where the employee works, such as their country's. They take its holidays instead of the system default's, on every team they are allocated to; with none (`holidayCalendarId` null) they follow the system default. A team's own calendar never decides a person's holidays. Employees_GetTimeAway shows the calendar the employee follows now. Requires the team-capacity-management feature flag; returns 404 when it is off or no employee has the id.")]
    [McpTool("Employees_ChangeHolidayCalendar", "Set employee holiday calendar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangeHolidayCalendar(Guid id, [FromBody] ChangeEmployeeHolidayCalendarRequest request, CancellationToken cancellationToken)
    {
        if (!await _dispatcher.Send(new EmployeeExistsQuery(id), cancellationToken))
            return NotFound();

        var result = await _dispatcher.Send(new ChangeEmployeeHolidayCalendarCommand(id, request.HolidayCalendarId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}/working-day-hours")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Employees)]
    [EndpointSummary("Set how many hours an employee's working day has.")]
    [EndpointDescription(
        "For someone whose working day is shorter or longer than the system's, such as a part-timer contracted for 4 hours a day; with none (`workingDayHours` null) they have the system's. The same on every team. Their allocation is a share of this day, so a part-timer allocated in full adds 4 hours, half a person-day of an 8-hour system day, to a team's capacity; time away entered in hours is a share of it. Employees_GetTimeAway shows the employee's own and the system's working day. Requires the team-capacity-management feature flag; returns 404 when it is off or no employee has the id.")]
    [McpTool("Employees_ChangeWorkingDayHours", "Set employee working day hours")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangeWorkingDayHours(Guid id, [FromBody] ChangeEmployeeWorkingDayHoursRequest request, CancellationToken cancellationToken)
    {
        if (!await _dispatcher.Send(new EmployeeExistsQuery(id), cancellationToken))
            return NotFound();

        var result = await _dispatcher.Send(new ChangeEmployeeWorkingDayHoursCommand(id, request.WorkingDayHours), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get the work items assigned to an employee.")]
    [EndpointDescription(
        "Takes the employee's UUID `id` and returns the backlog-level (Requirement tier) work items assigned to them across every workspace; epics, features and tasks are left out. `statusCategories` narrows them to any of the codes `Proposed`, `Active`, `Done` and `Removed`. `doneFrom` and `doneTo` keep only work items finished in that window, so either one leaves out work not yet done; a time without a zone is read as UTC. Needs the Work Items view permission.")]
    [McpTool("Employees_GetWorkItems", "List employee work items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetEmployeeWorkItems(
        Guid id,
        [FromQuery] WorkStatusCategory[]? statusCategories,
        [FromQuery] DateTime? doneFrom,
        [FromQuery] DateTime? doneTo,
        CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetEmployeeWorkItemsQuery(id, statusCategories, UtcQueryTime.ToInstant(doneFrom), UtcQueryTime.ToInstant(doneTo)), cancellationToken));
    }
}
