using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Work.WorkTypeLevels;
using Wayd.Work.Application.WorkTypeLevels.Commands;
using Wayd.Work.Application.WorkTypeLevels.Dtos;
using Wayd.Work.Application.WorkTypeLevels.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-type-levels")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Work, "wayd-work")]
public class WorkTypeLevelsController : ControllerBase
{
    private readonly ILogger<WorkTypeLevelsController> _logger;
    private readonly IDispatcher _dispatcher;

    public WorkTypeLevelsController(ILogger<WorkTypeLevelsController> logger, IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkTypeLevels)]
    [EndpointSummary("Get a list of all work type levels.")]
    [EndpointDescription("The levels that order work types into a hierarchy, each with an integer `id`, its `tier` and its `order` within the tier, where 1 is the top level (Epics above Features, say): a Portfolio-tier parent must be at a higher level than a Portfolio-tier child. Listed by tier, and within a tier from the bottom level up. A work type's level sets its tier.")]
    [McpTool("WorkTypeLevels_GetWorkTypeLevels", "List work type levels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkTypeLevelDto>>> GetList(CancellationToken cancellationToken)
    {
        var levels = await _dispatcher.Send(new GetWorkTypeLevelsQuery(), cancellationToken);

        return Ok(levels.OrderBy(l => l.Tier.Id).ThenByDescending(s => s.Order));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkTypeLevels)]
    [EndpointSummary("Get work type level details using the id.")]
    [McpTool("WorkTypeLevels_GetWorkTypeLevel", "Get work type level")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkTypeLevelDto>> GetById(int id)
    {
        var backlogLevel = await _dispatcher.Send(new GetWorkTypeLevelQuery(id));

        return backlogLevel is not null
            ? Ok(backlogLevel)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.WorkTypeLevels)]
    [EndpointSummary("Create a work type level.")]
    [EndpointDescription("Adds a Portfolio-tier level, the only tier that takes new levels, at the bottom of the tier; reorder it with `WorkTypeLevels_Reorder`. Level names must be unique. Returns the new level's integer id.")]
    [McpTool("WorkTypeLevels_Create", "Create a work type level", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Int))]
    public async Task<ActionResult> Create(CreateWorkTypeLevelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateWorkTypeLevelCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.WorkTypeLevels)]
    [EndpointSummary("Update a work type level.")]
    [EndpointDescription("Renames a level of any tier or changes its description; its tier and order do not change here. Level names must be unique. `id` must match `id` in the body. A whole-record overwrite: an omitted description is cleared, so read the level first and pass back what should stay.")]
    [McpTool("WorkTypeLevels_Update", "Update a work type level")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(int id, UpdateWorkTypeLevelRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateWorkTypeLevelCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }


    [HttpPut("order")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.WorkTypeLevels)]
    [EndpointSummary("Update the order of portfolio tier work type levels.")]
    [EndpointDescription("`levels` maps each Portfolio-tier level's id to its new position, 1 being the top, as a JSON object: `{ \"levels\": { \"12\": 1, \"14\": 2 } }`. Only Portfolio-tier levels can be reordered. Levels left out keep their position, and the call is refused if two Portfolio levels would end up at the same position, so send every level whose position changes.")]
    [McpTool("WorkTypeLevels_Reorder", "Reorder portfolio work type levels")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateOrder([FromBody] UpdateWorkTypeLevelsOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UpdateWorkTypeLevelsOrderCommand(request.Levels), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
