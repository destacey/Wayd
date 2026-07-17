using CsvHelper;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.Risks.Commands;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.Risks;

namespace Wayd.Web.Api.Controllers.Planning;

[Route("api/planning/risks")]
[ApiVersionNeutral]
[ApiController]
public class RisksController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly ICsvService _csvService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RisksController(IDispatcher dispatcher, ICsvService csvService, IDateTimeProvider dateTimeProvider)
    {
        _dispatcher = dispatcher;
        _csvService = csvService;
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [OpenApiOperation("Get a list of risks.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskListDto>>> GetList(CancellationToken cancellationToken, bool includeClosed = false)
    {
        var risks = await _dispatcher.Send(new GetRisksQuery(includeClosed), cancellationToken);
        return Ok(risks);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [OpenApiOperation("Get risk details using the Id or key.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskDetailsDto>> GetRisk(string idOrKey, CancellationToken cancellationToken)
    {
        var risk = await _dispatcher.Send(new GetRiskQuery(idOrKey), cancellationToken);

        return risk is not null
            ? Ok(risk)
            : NotFound();
    }

    [HttpGet("me")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [OpenApiOperation("Get a list of open risks assigned to me.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskListDto>>> GetMyRisks(CancellationToken cancellationToken)
    {
        var risks = await _dispatcher.Send(new GetMyRisksQuery(), cancellationToken);
        return Ok(risks);
    }

    [HttpPost()]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Risks)]
    [OpenApiOperation("Create a risk.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateRiskRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRisk), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Risks)]
    [OpenApiOperation("Update a risk.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.RiskId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.RiskId), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Risks)]
    [OpenApiOperation("Import risks from a csv file.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var importedRisks = _csvService.ReadCsv<ImportRiskRequest>(file.OpenReadStream());

            List<ImportRiskDto> risks = [];
            var validator = new ImportRiskRequestValidator(_dateTimeProvider);
            foreach (var risk in importedRisks)
            {
                var validationResults = await validator.ValidateAsync(risk, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        if (error.PropertyName != "RecordId")
                        {
                            error.ErrorMessage = $"{error.ErrorMessage} (Record Id: {risk.ImportId})";
                            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                        }
                    }
                    return UnprocessableEntity(validationResults);
                }
                else
                {
                    risks.Add(risk.ToImportRiskDto());
                }
            }

            if (risks.Count == 0)
                return BadRequest(ProblemDetailsExtensions.ForBadRequest("No risks imported.", HttpContext));

            var result = await _dispatcher.Send(new ImportRisksCommand(risks), cancellationToken);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            // TODO: Convert this to a validation problem details
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.ToString(), HttpContext));
        }
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [OpenApiOperation("Get a list of all risk statuses.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskStatusDto>>> GetStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRiskStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("categories")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [OpenApiOperation("Get a list of all risk categories.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskCategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRiskCategoriesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("grades")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Risks)]
    [OpenApiOperation("Get a list of all risk grades.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RiskGradeDto>>> GetGrades(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRiskGradesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }
}
