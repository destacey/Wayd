using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Admin.StatusWorkflows;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/workflow-assignments")]
[ApiVersionNeutral]
[ApiController]
public class WorkflowAssignmentsController(ILogger<WorkflowAssignmentsController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<WorkflowAssignmentsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Get which status workflow governs each scope.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkflowAssignmentDto>>> GetWorkflowAssignments([FromQuery] string? ownerType, CancellationToken cancellationToken)
    {
        var assignments = await _dispatcher.Send(new GetWorkflowAssignmentsQuery(ownerType), cancellationToken);

        return Ok(assignments);
    }

    [HttpGet("{id}/remap-preview")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Preview how each status would land in the target workflow, before anything is committed.", "")]
    [ProducesResponseType(typeof(StatusRemapPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StatusRemapPreviewDto>> PreviewRemap(Guid id, [FromQuery] Guid targetWorkflowId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new PreviewStatusRemapQuery(id, targetWorkflowId), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/reassign")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Move a scope onto another status workflow and migrate its records.", "")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<int>> Reassign(Guid id, [FromBody] ReassignWorkflowRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReassignWorkflowCommand(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
