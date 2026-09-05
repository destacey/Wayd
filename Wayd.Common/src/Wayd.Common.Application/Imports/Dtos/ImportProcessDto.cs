using NodaTime;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Imports.Dtos;

/// <summary>
/// One import run, as the person who submitted it sees it.
/// </summary>
/// <remarks>
/// Carries the display name and the atomicity alongside the counts because the run alone cannot explain
/// itself: "0 of 40 applied" reads as a bug until you know the import is all-or-nothing.
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
    string? Error)
{
    /// <summary>Rows neither applied nor rejected — what a resume would pick up.</summary>
    public int UnappliedRowCount => TotalRowCount - SucceededRowCount - FailedRowCount;
}

/// <summary>A page of runs, with the total so a UI can page without a second call.</summary>
public sealed record ImportProcessPageDto(
    IReadOnlyList<ImportProcessDto> Processes,
    int TotalCount,
    int PageNumber,
    int PageSize);
