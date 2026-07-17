using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Work.WorkStatuses;
using Wayd.Work.Application.WorkStatuses.Dtos;
using Wayd.Work.Application.WorkStatuses.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-statuses")]
[ApiVersionNeutral]
[ApiController]
public class WorkStatusesController : ControllerBase
{
    private readonly ILogger<WorkStatusesController> _logger;
    private readonly IDispatcher _dispatcher;

    public WorkStatusesController(ILogger<WorkStatusesController> logger, IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkStatuses)]
    [OpenApiOperation("Get a list of all work statuss.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkStatusDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var workStatuses = await _dispatcher.Send(new GetWorkStatusesQuery(includeInactive), cancellationToken);
        return Ok(workStatuses.OrderBy(s => s.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkStatuses)]
    [OpenApiOperation("Get work status details using the id.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkStatusDto>> GetById(int id)
    {
        var workStatus = await _dispatcher.Send(new GetWorkStatusQuery(id));

        return workStatus is not null
            ? Ok(workStatus)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.WorkStatuses)]
    [OpenApiOperation("Create a work status.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Int))]
    public async Task<ActionResult> Create(CreateWorkStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateWorkStatusCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.WorkStatuses)]
    [OpenApiOperation("Update a work status.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(int id, UpdateWorkStatusRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateWorkStatusCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    //[HttpDelete("{id}")]
    //[MustHavePermission(ApplicationAction.Delete, ApplicationResource.WorkStatuses)]
    //[OpenApiOperation("Delete a work status.", "")]
    //public async Task<ActionResult> Delete(int id)
    //{
    //    throw new NotImplementedException();
    //}
}
