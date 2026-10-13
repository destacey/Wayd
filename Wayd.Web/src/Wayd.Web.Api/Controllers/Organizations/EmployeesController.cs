using CsvHelper;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
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
    [OpenApiOperation("Get a list of all employees.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EmployeeListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var employees = await _dispatcher.Send(new GetEmployeesQuery(includeInactive), cancellationToken);
        return Ok(employees.OrderBy(e => e.LastName));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [OpenApiOperation("Get employee details using the Id or key.", "")]
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
    [OpenApiOperation("Create an employee.", "")]
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
    [OpenApiOperation("Import employees from a csv file. Returns the run — 200 once it has finished, 202 while it is still queued or running.", "")]
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
    [OpenApiOperation("Update an employee.", "")]
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

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Employees)]
    [OpenApiOperation("Delete an employee.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(string id)
    {
        var result = await _dispatcher.Send(new DeleteEmployeeCommand(Guid.Parse(id)));

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/direct-reports")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [OpenApiOperation("Get a list of direct reports for an employee.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EmployeeListDto>>> GetDirectReports(Guid id, CancellationToken cancellationToken)
    {
        var directReports = await _dispatcher.Send(new GetDirectReportsQuery(id), cancellationToken);
        return Ok(directReports.OrderBy(e => e.LastName));
    }


    [HttpPost("{id}/remove-invalid")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Employees)]
    [OpenApiOperation("Remove invalid employee record from employee list.", "")]
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
    [OpenApiOperation("Get team memberships for an employee.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetTeamMemberships(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetEmployeeTeamMembershipsQuery(id), cancellationToken));
    }

    [HttpGet("{id:guid}/allocations")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Employees)]
    [OpenApiOperation(
        "Get how an employee's time is allocated to teams over time.",
        "Returns every allocation across all teams in date order: the team, the role the time is spent in when one was chosen, the date range (`end` is null when it has no end) and the `percent` of the employee's time. An employee's allocations never add up to more than 100% on any day. `canManage` says whether the caller may change that team's allocations. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeAllocationDto>>> GetAllocations(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetEmployeeAllocationsQuery(id), cancellationToken));
    }

    [HttpPut("{id:guid}/allocations")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation(
        "Set the share of an employee's time one or more teams have over date ranges.",
        "Each change replaces what its team and role have over its date range and leaves other days as they were, so a change inside a longer allocation splits it; a `percent` of 0 ends the allocation for the range. The changes apply together or not at all, and fail if any day would then exceed 100% across all teams. The caller must be a member of every team named, or of its team of teams, unless they hold the Administer Teams permission. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangeAllocations(Guid id, [FromBody] ChangeEmployeeAllocationsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToChangeEmployeeAllocationsCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/allocations/move")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation(
        "Move a share of an employee's time from one team and role to another over a date range.",
        "Lowers the source and raises the target by `percent` on every day of the range as one change, so a fully allocated employee can be moved between teams. Fails if the source has less than `percent` on any day of the range. The caller must be a member of both teams, or of their teams of teams, unless they hold the Administer Teams permission. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MoveAllocation(Guid id, [FromBody] MoveEmployeeAllocationRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToMoveEmployeeAllocationCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [OpenApiOperation("Get the work items assigned to an employee.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetEmployeeWorkItems(
        Guid id,
        [FromQuery] WorkStatusCategory[]? statusCategories,
        [FromQuery] DateTime? doneFrom,
        [FromQuery] DateTime? doneTo,
        CancellationToken cancellationToken)
    {
        Instant? doneFromInstant = null;
        Instant? doneToInstant = null;

        if (doneFrom.HasValue)
        {
            var df = doneFrom.Value;
            df = df.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(df, DateTimeKind.Utc) : df.ToUniversalTime();
            doneFromInstant = Instant.FromDateTimeUtc(df);
        }

        if (doneTo.HasValue)
        {
            var dt = doneTo.Value;
            dt = dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
            doneToInstant = Instant.FromDateTimeUtc(dt);
        }

        return Ok(await _dispatcher.Send(new GetEmployeeWorkItemsQuery(id, statusCategories, doneFromInstant, doneToInstant), cancellationToken));
    }
}
