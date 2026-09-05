using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Web.Api.Extensions;

namespace Wayd.Web.Api.Controllers.Imports;

/// <summary>
/// Tracking and control for a submitted import, whatever kind of file it was.
/// </summary>
/// <remarks>
/// Deliberately not per domain area: a caller that submitted a file has one id and wants one place to ask
/// about it.
/// <para>
/// Two gates, because they answer different questions. The attribute gates reaching the imports area at
/// all, the way every other Settings area is gated. Which imports a holder may then see is a separate
/// question the attribute cannot answer: it depends on the run's own definition, which names whatever
/// gates submitting that kind of file, and is only known once the handler has read the run.
/// </para>
/// </remarks>
[Route("api/imports")]
[ApiVersionNeutral]
[ApiController]
[MustHavePermission(ApplicationAction.View, ApplicationResource.Imports)]
public class ImportsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [OpenApiOperation("Get a page of import runs, newest first.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportProcessPageDto>> GetList(
        CancellationToken cancellationToken,
        ImportProcessStatus? status = null,
        string? importType = null,
        string? submittedByUserId = null,
        int pageNumber = 1,
        int pageSize = 100)
    {
        var result = await _dispatcher.Send(
            new GetImportProcessesQuery(status, importType, submittedByUserId, pageNumber, pageSize),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // Ahead of the {id:guid} routes for readability only - the constraint is what keeps "definitions" from
    // binding as an id.
    [HttpGet("definitions")]
    [OpenApiOperation("Get the import types the caller may submit.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ImportDefinitionDto>>> GetDefinitions(CancellationToken cancellationToken) =>
        Ok(await _dispatcher.Send(new GetImportDefinitionsQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    [OpenApiOperation("Get the status and counts of an import.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportProcessDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetImportProcessQuery(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id:guid}/rows")]
    [OpenApiOperation("Get a page of row outcomes for an import.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportProcessRowPageDto>> GetRows(
        Guid id,
        CancellationToken cancellationToken,
        ImportRowStatus? status = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        var result = await _dispatcher.Send(
            new GetImportProcessRowsQuery(id, status, pageNumber, pageSize), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/cancel")]
    [OpenApiOperation("Stop an import that is still running.", "")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CancelImportProcessCommand(id), cancellationToken);

        return result.IsSuccess
            ? Accepted()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/resume")]
    [OpenApiOperation("Queue an import again to apply the rows it never reached.", "")]
    [ProducesResponseType(typeof(ResumedImport), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResumedImport>> Resume(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ResumeImportProcessCommand(id), cancellationToken);

        return result.IsSuccess
            ? Accepted(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/retry-failed")]
    [OpenApiOperation("Queue an import again, this time also reattempting the rows it rejected.", "")]
    [ProducesResponseType(typeof(ResumedImport), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResumedImport>> RetryFailed(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new ResumeImportProcessCommand(id, RetryFailedRows: true), cancellationToken);

        return result.IsSuccess
            ? Accepted(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
