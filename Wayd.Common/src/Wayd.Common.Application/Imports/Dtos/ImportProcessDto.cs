using NodaTime;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports.Dtos;

/// <summary>
/// One import run, as the person who submitted it sees it.
/// </summary>
/// <remarks>
/// Carries the display name and the atomicity alongside the counts because the run alone cannot explain
/// itself: "0 of 40 applied" reads as a bug until you know the import is all-or-nothing.
/// <para>
/// <c>CanManage</c> mirrors the authorization rule onto the read side, as the PPM projections do: a
/// reader holding oversight of every import type may still not act on this one, and the UI has no other
/// way to know which of its buttons would be refused.
/// </para>
/// </remarks>
public sealed record ImportProcessDto(
    Guid Id,
    string ImportType,
    string DisplayName,
    ImportAtomicity Atomicity,
    ImportProcessStatus Status,
    Guid? SubmissionGroupId,
    string SubmittedByUserId,
    string? SubmittedByName,
    Instant SubmittedOn,
    Instant? StartedOn,
    Instant? CompletedOn,
    Instant? LastProgressOn,
    int TotalRowCount,
    int SucceededRowCount,
    int FailedRowCount,
    string? Error,
    bool CanManage)
{
    /// <summary>Rows neither applied nor rejected — what a resume would pick up.</summary>
    public int UnappliedRowCount => TotalRowCount - SucceededRowCount - FailedRowCount;

    /// <summary>Whether the run has finished, so a caller polling it knows when to stop.</summary>
    public bool IsTerminal => ImportProcess.IsTerminalStatus(Status);
}

/// <summary>A page of runs, with the total so a UI can page without a second call.</summary>
public sealed record ImportProcessPageDto(
    IReadOnlyList<ImportProcessDto> Processes,
    int TotalCount,
    int PageNumber,
    int PageSize);
