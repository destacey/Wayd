using CsvHelper;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Programs.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.Programs;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/[controller]")]
[ApiVersionNeutral]
[ApiController]
public class ProgramsController(ILogger<ProgramsController> logger, IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly ILogger<ProgramsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Programs)]
    [OpenApiOperation("Get a list of programs.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]

    public async Task<ActionResult<IEnumerable<ProgramListDto>>> GetPrograms([FromQuery] int[]? status, [FromQuery] Guid? portfolioId, CancellationToken cancellationToken)
    {
        ProgramStatus[]? filter = status is { Length: > 0 }
            ? [.. status.Select(s => (ProgramStatus)s)]
            : null;

        IdOrKey? portfolioIdOrKey = portfolioId.HasValue
            ? new IdOrKey(portfolioId.Value)
            : null;

        var programs = await _dispatcher.Send(new GetProgramsQuery(StatusFilter: filter, PortfolioIdOrKey: portfolioIdOrKey), cancellationToken);

        return programs is not null
            ? Ok(programs)
            : NotFound();
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Programs)]
    [OpenApiOperation("Get program details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProgramDetailsDto>> GetProgram(string idOrKey, CancellationToken cancellationToken)
    {
        var program = await _dispatcher.Send(new GetProgramQuery(idOrKey), cancellationToken);

        return program is not null
            ? Ok(program)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Programs)]
    [OpenApiOperation("Create a program.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateProgramRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProgramCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProgram), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Programs)]
    [OpenApiOperation("Import programs from a csv file.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var importedPrograms = _csvService.ReadCsv<ImportProgramRequest>(file.OpenReadStream());

            List<ImportProgramDto> programs = [];
            var validator = new ImportProgramRequestValidator();
            foreach (var program in importedPrograms)
            {
                var validationResults = await validator.ValidateAsync(program, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Name: {program.Name})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(validationResults);
                }

                programs.Add(program.ToImportProgramDto());
            }

            if (programs.Count == 0)
                return BadRequest(ProblemDetailsExtensions.ForBadRequest("No programs imported.", HttpContext));

            var result = await _dispatcher.Send(new ImportProgramsCommand(programs), cancellationToken);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.ToString(), HttpContext));
        }
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Programs)]
    [OpenApiOperation("Update a program.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateProgramRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateProgramCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Programs)]
    [OpenApiOperation("Activate a program.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateProgramCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/complete")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Programs)]
    [OpenApiOperation("Complete a program.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CompleteProgramCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/cancel")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Programs)]
    [OpenApiOperation("Cancel a program.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CancelProgramCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Programs)]
    [OpenApiOperation("Delete a program.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProgramCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }



    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Programs)]
    [OpenApiOperation("Get a list of all program statuses.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProgramStatusDto>>> GetProgramStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetProgramStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("{idOrKey}/projects")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Programs)]
    [OpenApiOperation("Get a list of projects.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectListDto>>> GetProjects(string idOrKey, [FromQuery] int[]? status, CancellationToken cancellationToken)
    {
        ProjectStatus[]? filter = status is { Length: > 0 }
            ? [.. status.Select(s => (ProjectStatus)s)]
            : null;

        var projects = await _dispatcher.Send(new GetProjectsQuery(StatusFilter: filter, ProgramIdOrKey: idOrKey), cancellationToken);

        return projects is not null
            ? Ok(projects)
            : NotFound();
    }
}
