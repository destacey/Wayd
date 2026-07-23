using CsvHelper;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Employees.Queries;
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
public class EmployeesController(ILogger<EmployeesController> logger, IDispatcher dispatcher, ICsvService csvService) : ControllerBase
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
    [OpenApiOperation("Import employees from a csv file.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var importedEmployees = _csvService.ReadCsv<ImportEmployeeRequest>(file.OpenReadStream());

            List<ImportEmployeeDto> employees = [];
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
                    return UnprocessableEntity(validationResults);
                }

                employees.Add(employee.ToImportEmployeeDto());
            }

            if (employees.Count == 0)
                return BadRequest(ProblemDetailsExtensions.ForBadRequest("No employees imported.", HttpContext));

            var result = await _dispatcher.Send(new ImportEmployeesCommand(employees), cancellationToken);

            return result.IsSuccess
                ? NoContent()
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
