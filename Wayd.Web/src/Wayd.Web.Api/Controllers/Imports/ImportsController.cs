using Microsoft.AspNetCore.Authorization;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.Controllers.Imports;

/// <summary>
/// Tracking and control for a submitted import, whatever kind of file it was.
/// </summary>
/// <remarks>
/// Deliberately not per domain area: a caller that submitted a file has one id and wants one place to ask
/// about it.
/// <para>
/// There is deliberately no import permission of its own. Being allowed to submit a kind of file is what
/// entitles you to see how it went, so the gate is the one the run's own definition names — which is only
/// knowable once the handler has read the run, and so cannot be an attribute. A separate "view imports"
/// claim would be able to withhold from someone the result of an import they just ran themselves.
/// </para>
/// <para>
/// <c>Authorize</c> still has to be here: no fallback policy is registered, so an action without it is
/// reachable anonymously.
/// </para>
/// </remarks>
[Route(Route)]
[ApiVersionNeutral]
[ApiController]
[Authorize]
public class ImportsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Also where every import submission points a caller to follow its run.</summary>
    public const string Route = "api/imports";

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [OpenApiOperation(
        "List import runs, newest first, as `processes` with a `totalCount`.",
        "Only runs of the kinds you may submit are included, or every kind if you hold View Imports. A run carries `status` (Queued, Processing, Cancelling, Succeeded, PartiallySucceeded, Failed, Cancelled), `isTerminal`, `isPreflight`, `atomicity` (PerRow; Atomic, where one rejected row means nothing is written; or PerGroup, where the rows sharing a group — named by `groupNoun`, such as every dependency of one product — apply together or not at all and the other groups are kept), and the counts `totalRowCount`, `succeededRowCount`, `failedRowCount` and `unappliedRowCount`. `canManage` says whether you may cancel, resume, retry or apply it.")]
    [McpTool("Imports_GetList", "List import runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportProcessPageDto>> GetList(
        CancellationToken cancellationToken,
        ImportProcessStatus? status = null,
        string? importType = null,
        string? submittedByUserId = null,
        Guid? submissionGroupId = null,
        int pageNumber = 1,
        int pageSize = 100)
    {
        var result = await _dispatcher.Send(
            new GetImportProcessesQuery(status, importType, submittedByUserId, submissionGroupId, pageNumber, pageSize),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // Ahead of the {id:guid} routes for readability only - the constraint is what keeps "definitions" from
    // binding as an id.
    [HttpGet("definitions")]
    [OpenApiOperation(
        "List the kinds of import you may see.",
        "Each has its `key` (what `Imports_GetList` filters on), `displayName`, `atomicity` (PerRow; Atomic, where one rejected row means nothing is written; or PerGroup, where the rows sharing a group — named by `groupNoun`, such as every dependency of one product — apply together or not at all and the other groups are kept), `maxRows` and `preflightMaxRows` (the most rows one file may hold for each), and `canSubmit`, whether you may submit that kind of file and act on its runs.")]
    [McpTool("Imports_GetDefinitions", "List import types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ImportDefinitionDto>>> GetDefinitions(CancellationToken cancellationToken) =>
        Ok(await _dispatcher.Send(new GetImportDefinitionsQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    [OpenApiOperation(
        "Get one import run's status and counts.",
        "Poll this until `isTerminal` is true to follow a run that was still going when it was submitted. A run carries `status` (Queued, Processing, Cancelling, Succeeded, PartiallySucceeded, Failed, Cancelled), `isTerminal`, `isPreflight`, `atomicity` (PerRow; Atomic, where one rejected row means nothing is written; or PerGroup, where the rows sharing a group — named by `groupNoun`, such as every dependency of one product — apply together or not at all and the other groups are kept), and the counts `totalRowCount`, `succeededRowCount`, `failedRowCount` and `unappliedRowCount`. `canManage` says whether you may cancel, resume, retry or apply it. `error` explains a run that could not continue; a rejected row's reason is on the row, from `Imports_GetRows`.")]
    [McpTool("Imports_GetById", "Get import run")]
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
    [OpenApiOperation(
        "Get a page of an import run's row outcomes, as `rows` with a `totalCount`.",
        "Each row has its `importId` (the file's ImportId column, or its position when the file had none), `rowNumber`, `status` (Pending, Succeeded, Failed, Cancelled), the `error` a rejected row was refused for, any `warning` a successful row recorded, and `createdEntityId`. In a preflight, Succeeded means the row would have been imported. Filter by `status: Failed` to see only what needs fixing.")]
    [McpTool("Imports_GetRows", "Get import run rows")]
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
    [OpenApiOperation(
        "Stop an import run that is still going.",
        "A request, not an undo: the worker stops at its next batch boundary, and rows already applied stay applied. The rest can be picked up later with `Imports_Resume`. Refused for a run that has already finished.")]
    [McpTool("Imports_Cancel", "Stop import run")]
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
    [OpenApiOperation(
        "Queue a finished import run again to apply the rows it never reached — after it was stopped, or failed partway.",
        "Rows that succeeded are never reapplied, and rejected rows stay rejected (use `Imports_RetryFailed` for those). Answers with `queuedRowCount` and `skippedRowCount`, the rows whose data the 30-day retention window already discarded. Refused for a run still going, and for a preflight, which is imported with `Imports_Apply` instead.")]
    [McpTool("Imports_Resume", "Resume import run")]
    [ProducesResponseType(typeof(ResumedImport), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResumedImport>> Resume(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ResumeImportProcessCommand(id), cancellationToken);

        return result.IsSuccess
            ? Accepted(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/apply")]
    [OpenApiOperation(
        "Import, for real, the rows a finished preflight checked — without sending the file again.",
        "Every row is submitted, including ones the preflight rejected, and each is checked again against the data as it is now, so the import may still refuse a row the preflight passed. Applying the same preflight twice rejects the records the first import created as duplicates, or, for an import with no natural key such as deployments, creates them a second time; check `appliedImportProcessId` on the preflight first. Refused once the preflight's rows pass the 30-day retention window. Answers with the new run once it has finished, or while it is still queued or running if it takes longer than a few seconds — check `isTerminal`, and poll `Imports_GetById` until it is true.")]
    [McpTool("Imports_Apply", "Import a checked file")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Apply(
        Guid id,
        [FromQuery] Guid? submissionGroupId,
        [FromServices] ImportSubmissionResponder responder,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ApplyImportPreflightCommand(id, submissionGroupId), cancellationToken);

        return result.IsSuccess
            ? await responder.Respond(this, result.Value, cancellationToken)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/retry-failed")]
    [OpenApiOperation(
        "Queue a finished import run again, reattempting its rejected rows as well as any it never reached.",
        "Fix whatever the rows were rejected for first — the rows are resubmitted exactly as they were, so a problem in the file itself needs a corrected file instead. Rows that succeeded are never reapplied. Answers with `queuedRowCount` and `skippedRowCount`. Refused for a run still going, and for a preflight.")]
    [McpTool("Imports_RetryFailed", "Retry rejected import rows")]
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
