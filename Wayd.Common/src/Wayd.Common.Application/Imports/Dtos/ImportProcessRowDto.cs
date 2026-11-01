using NodaTime;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Imports.Dtos;

/// <summary>
/// One row's outcome.
/// </summary>
/// <remarks>
/// The payload is deliberately absent: it is a verbatim copy of what was uploaded, the caller already has
/// the file, and a run's history is readable by anyone who can submit that import type — not only by the
/// person who submitted this one.
/// </remarks>
/// <param name="CreatedEntityId">The record this row created or changed.</param>
/// <param name="CreatedRecord">
/// Where to see that record — or, for one with no page of its own, the record it belongs to. Null when the
/// row created nothing, when that record or the one the link opens has since been deleted, and for a
/// preflight, whose records were rolled back.
/// </param>
public sealed record ImportProcessRowDto(
    Guid Id,
    string ImportId,
    int RowNumber,
    ImportRowStatus Status,
    Guid? CreatedEntityId,
    string? Error,
    string? Warning,
    Instant? AttemptedOn,
    ImportRecordLink? CreatedRecord = null);

/// <summary>A page of row outcomes, with the total so a UI can page without a second call.</summary>
public sealed record ImportProcessRowPageDto(
    IReadOnlyList<ImportProcessRowDto> Rows,
    int TotalCount,
    int PageNumber,
    int PageSize);
