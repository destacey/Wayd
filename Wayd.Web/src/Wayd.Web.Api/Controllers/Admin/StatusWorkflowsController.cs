using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Admin.StatusWorkflows;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/status-workflows")]
[ApiVersionNeutral]
[ApiController]
public class StatusWorkflowsController(ILogger<StatusWorkflowsController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<StatusWorkflowsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Get a list of status workflows.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StatusWorkflowListDto>>> GetStatusWorkflows([FromQuery] string? ownerType, [FromQuery] StatusWorkflowState? state, CancellationToken cancellationToken)
    {
        var workflows = await _dispatcher.Send(new GetStatusWorkflowsQuery(ownerType, state), cancellationToken);

        return Ok(workflows);
    }

    // Declared before the {idOrKey} route, or the literal would be captured by it.
    [HttpGet("owner-types")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Get the owner types a status workflow can be built for, with their alias vocabularies.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkflowOwnerTypeDto>>> GetOwnerTypes(CancellationToken cancellationToken)
    {
        var ownerTypes = await _dispatcher.Send(new GetWorkflowOwnerTypesQuery(), cancellationToken);

        return Ok(ownerTypes);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Get status workflow details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatusWorkflowDetailsDto>> GetStatusWorkflow(string idOrKey, CancellationToken cancellationToken)
    {
        var workflow = await _dispatcher.Send(new GetStatusWorkflowQuery(idOrKey), cancellationToken);

        return workflow is not null
            ? Ok(workflow)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Create a status workflow.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateStatusWorkflowRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateStatusWorkflowCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetStatusWorkflow), new { idOrKey = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Update a status workflow.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateStatusWorkflowRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateStatusWorkflowCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/clone")]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Copy a status workflow into a new editable draft.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Clone(Guid id, [FromBody] CloneStatusWorkflowRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCloneStatusWorkflowCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetStatusWorkflow), new { idOrKey = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/publish")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Publish a status workflow.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new PublishStatusWorkflowCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Archive a status workflow.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveStatusWorkflowCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #region Statuses

    [HttpPost("{id}/statuses")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Add a status to a status workflow.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> AddStatus(Guid id, [FromBody] AddWorkflowStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddWorkflowStatusCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetStatusWorkflow), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/statuses/{statusId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Rename a status in a status workflow.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> RenameStatus(Guid id, Guid statusId, [FromBody] RenameWorkflowStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToRenameWorkflowStatusCommand(id, statusId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/statuses/{statusId}/classification")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Change the category and well-known meaning of a status.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReclassifyStatus(Guid id, Guid statusId, [FromBody] ReclassifyWorkflowStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReclassifyWorkflowStatusCommand(id, statusId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/statuses/{statusId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Remove a status from a status workflow.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveStatus(Guid id, Guid statusId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveWorkflowStatusCommand(id, statusId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/statuses/reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [OpenApiOperation("Reorder the statuses in a status workflow.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderStatuses(Guid id, [FromBody] ReorderWorkflowStatusesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderWorkflowStatusesCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Statuses
}
