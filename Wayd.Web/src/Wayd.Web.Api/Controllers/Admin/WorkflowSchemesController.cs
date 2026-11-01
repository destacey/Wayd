using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Admin.StatusWorkflows;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/workflow-schemes")]
[ApiVersionNeutral]
[ApiController]
public class WorkflowSchemesController(ILogger<WorkflowSchemesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<WorkflowSchemesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Get the workflow schemes of owner types with record types.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkflowSchemeListDto>>> GetWorkflowSchemes([FromQuery] string? ownerType, CancellationToken cancellationToken)
    {
        var schemes = await _dispatcher.Send(new GetWorkflowSchemesQuery(ownerType), cancellationToken);

        return Ok(schemes);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Get a workflow scheme with its entries and the scopes that follow it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowSchemeDetailsDto>> GetWorkflowScheme(string idOrKey, CancellationToken cancellationToken)
    {
        var scheme = await _dispatcher.Send(new GetWorkflowSchemeQuery(new IdOrKey(idOrKey)), cancellationToken);

        return scheme is not null
            ? Ok(scheme)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Create a workflow scheme.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateWorkflowSchemeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateWorkflowSchemeCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetWorkflowScheme), new { idOrKey = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Edit a workflow scheme's name and description.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateWorkflowSchemeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateWorkflowSchemeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/clone")]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Copy a workflow scheme into a new owned one, for a scope that needs to differ.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Clone(Guid id, [FromBody] CloneWorkflowSchemeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCloneWorkflowSchemeCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetWorkflowScheme), new { idOrKey = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Delete a workflow scheme no scope follows.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteWorkflowSchemeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/entries/{recordTypeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Change which workflow one record type follows under a scheme, moving that type's records in every scope on it.")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<int>> SetEntry(Guid id, Guid recordTypeId, [FromBody] SetWorkflowSchemeEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToSetWorkflowSchemeEntryCommand(id, recordTypeId), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/default")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Change the workflow records follow when their type has no entry, moving them in every scope on the scheme.")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<int>> ChangeDefault(Guid id, [FromBody] ChangeWorkflowSchemeDefaultRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToChangeWorkflowSchemeDefaultCommand(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/entries/{recordTypeId}/preview")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Preview what changing a record type's workflow under a scheme would move, before anything is committed.")]
    [EndpointDescription("Leave workflowId out to preview removing the entry, so the type follows the scheme's default.")]
    [ProducesResponseType(typeof(WorkflowSchemeChangePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowSchemeChangePreviewDto>> PreviewEntry(Guid id, Guid recordTypeId, [FromQuery] Guid? workflowId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new PreviewWorkflowSchemeEntryQuery(id, recordTypeId, workflowId), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/default/preview")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Preview what changing a scheme's default workflow would move, before anything is committed.")]
    [ProducesResponseType(typeof(WorkflowSchemeChangePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowSchemeChangePreviewDto>> PreviewDefault(Guid id, [FromQuery] Guid workflowId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new PreviewWorkflowSchemeDefaultQuery(id, workflowId), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
