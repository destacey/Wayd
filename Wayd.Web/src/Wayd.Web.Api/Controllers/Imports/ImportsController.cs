using Microsoft.AspNetCore.Authorization;
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
/// about it. There is no permission attribute because there is no single import permission — the handler
/// authorizes on whatever gates submitting that kind of file, which it can only know once it has read the
/// run. <c>Authorize</c> still has to be here: no fallback policy is registered, so an action without it
/// is reachable anonymously.
/// </remarks>
[Route("api/imports")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
public class ImportsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet("{id}")]
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

    [HttpGet("{id}/rows")]
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

    [HttpPost("{id}/cancel")]
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

    [HttpPost("{id}/resume")]
    [OpenApiOperation("Queue an import again to apply the rows it never reached.", "")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResumedImport>> Resume(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ResumeImportProcessCommand(id), cancellationToken);

        return result.IsSuccess
            ? Accepted(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/retry-failed")]
    [OpenApiOperation("Queue an import again, this time also reattempting the rows it rejected.", "")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
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
