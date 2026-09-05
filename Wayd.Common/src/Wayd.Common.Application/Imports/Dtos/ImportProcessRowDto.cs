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
public sealed record ImportProcessRowDto(
    Guid Id,
    string ImportId,
    int RowNumber,
    ImportRowStatus Status,
    Guid? CreatedEntityId,
    string? Error,
    string? Warning,
    Instant? AttemptedOn);

/// <summary>A page of row outcomes, with the total so a UI can page without a second call.</summary>
public sealed record ImportProcessRowPageDto(
    IReadOnlyList<ImportProcessRowDto> Rows,
    int TotalCount,
    int PageNumber,
    int PageSize);
