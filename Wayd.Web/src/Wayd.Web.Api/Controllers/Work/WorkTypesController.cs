using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Work.WorkTypes;
using Wayd.Work.Application.WorkTypes.Dtos;
using Wayd.Work.Application.WorkTypes.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-types")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Work, "wayd-work")]
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
    [EndpointSummary("Get a list of all work types.")]
    [EndpointDescription("The kinds of work item, such as Epic, Feature, User Story or Bug, by name, each with an integer `id` and its `level`, which sets its tier (Portfolio, Requirement, Task or Other) and so where its work items appear. Active work types only unless `includeInactive` is true.")]
    [McpTool("WorkTypes_GetWorkTypes", "List work types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkTypeDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var workTypes = await _dispatcher.Send(new GetWorkTypesQuery(includeInactive), cancellationToken);
        return Ok(workTypes.OrderBy(s => s.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkTypes)]
    [EndpointSummary("Get work type details using the id.")]
    [McpTool("WorkTypes_GetWorkType", "Get work type")]
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
    [EndpointSummary("Create a work type.")]
    [EndpointDescription("Returns the new work type's integer id. The name must be unique and can never be changed; `levelId` is a work type level's id from `WorkTypeLevels_GetWorkTypeLevels`. Work types are normally created by syncing a work process from the source system, which reuses an existing work type of the same name.")]
    [McpTool("WorkTypes_Create", "Create a work type", Destructive = false)]
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
    [EndpointSummary("Update a work type.")]
    [EndpointDescription("Changes a work type's description and its level (`levelId`, from `WorkTypeLevels_GetWorkTypeLevels`), which moves it to that level's tier and so changes where its work items appear: the Portfolio tier can be parents and take projects, the Requirement tier is a team's backlog and sprint work, and the Other tier is left out of backlog and sprint views. Its name cannot change. `id` must match `id` in the body. A whole-record overwrite: an omitted description is cleared, so read the work type first and pass back what should stay.")]
    [McpTool("WorkTypes_Update", "Update a work type")]
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
}
