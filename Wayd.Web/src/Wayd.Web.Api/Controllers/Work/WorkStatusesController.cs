using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Work.WorkStatuses;
using Wayd.Work.Application.WorkStatuses.Dtos;
using Wayd.Work.Application.WorkStatuses.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-statuses")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Work, "wayd-work")]
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
    [EndpointSummary("Get a list of all work statuses.")]
    [EndpointDescription("The statuses work items can be in, such as New, In Progress or Done, by name, each with an integer `id`. A status has no category of its own: each workflow places it in a work status category. Active statuses only unless `includeInactive` is true.")]
    [McpTool("WorkStatuses_GetWorkStatuses", "List work statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkStatusDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var workStatuses = await _dispatcher.Send(new GetWorkStatusesQuery(includeInactive), cancellationToken);
        return Ok(workStatuses.OrderBy(s => s.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkStatuses)]
    [EndpointSummary("Get work status details using the id.")]
    [McpTool("WorkStatuses_GetWorkStatus", "Get work status")]
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
    [EndpointSummary("Create a work status.")]
    [EndpointDescription("Returns the new status's integer id. The name must be unique and can never be changed. Statuses are normally created by syncing a work process from the source system, which reuses an existing status of the same name.")]
    [McpTool("WorkStatuses_Create", "Create a work status", Destructive = false)]
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
    [EndpointSummary("Update a work status.")]
    [EndpointDescription("Changes a status's description, the only field that can change; its name cannot. `id` must match `id` in the body. A whole-record overwrite: an omitted description is cleared, so read the status first and pass back what should stay.")]
    [McpTool("WorkStatuses_Update", "Update a work status")]
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
}
