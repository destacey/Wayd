using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Admin.StatusWorkflows;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/record-types")]
[ApiVersionNeutral]
[ApiController]
public class RecordTypesController(ILogger<RecordTypesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<RecordTypesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Get the record types of workflow owner types that have them.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RecordTypeDto>>> GetRecordTypes([FromQuery] string? ownerType, CancellationToken cancellationToken)
    {
        var recordTypes = await _dispatcher.Send(new GetRecordTypesQuery(ownerType), cancellationToken);

        return Ok(recordTypes);
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Define a record type for an owner type that has them.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateRecordTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateRecordTypeCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRecordTypes), null, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Rename an owned record type.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Rename(Guid id, [FromBody] RenameRecordTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToRenameRecordTypeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Let records be given a record type again.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetRecordTypeActiveCommand(id, IsActive: true), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/deactivate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StatusWorkflows)]
    [EndpointSummary("Stop records being given a record type. Records that have it keep it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetRecordTypeActiveCommand(id, IsActive: false), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
