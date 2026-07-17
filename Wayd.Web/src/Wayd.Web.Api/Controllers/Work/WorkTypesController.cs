using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Work.WorkTypes;
using Wayd.Work.Application.WorkTypes.Dtos;
using Wayd.Work.Application.WorkTypes.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-types")]
[ApiVersionNeutral]
[ApiController]
public class WorkTypesController : ControllerBase
{
    private readonly ILogger<WorkTypesController> _logger;
    private readonly IDispatcher _dispatcher;

    public WorkTypesController(ILogger<WorkTypesController> logger, IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkTypes)]
    [OpenApiOperation("Get a list of all work types.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkTypeDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var workTypes = await _dispatcher.Send(new GetWorkTypesQuery(includeInactive), cancellationToken);
        return Ok(workTypes.OrderBy(s => s.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkTypes)]
    [OpenApiOperation("Get work type details using the id.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkTypeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var workType = await _dispatcher.Send(new GetWorkTypeQuery(id), cancellationToken);

        return workType is not null
            ? Ok(workType)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.WorkTypes)]
    [OpenApiOperation("Create a work type.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Int))]
    public async Task<ActionResult> Create(CreateWorkTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateWorkTypeCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.WorkTypes)]
    [OpenApiOperation("Update a work type.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(int id, UpdateWorkTypeRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateWorkTypeCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    //[HttpDelete("{id}")]
    //[MustHavePermission(ApplicationAction.Delete, ApplicationResource.WorkTypes)]
    //[OpenApiOperation("Delete a work type.", "")]
    //public async Task<ActionResult> Delete(int id)
    //{
    //    throw new NotImplementedException();
    //}
}
