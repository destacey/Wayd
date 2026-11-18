using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.Incidents.Dtos;

/// <summary>
/// A single incident row.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ImportId"/> is the caller's key for the row inside this file. It is required, and it is
/// not stored on the incident. A blank cell fails the row rather than being replaced with the row number.
/// </para>
/// <para>
/// <see cref="Severity"/> is a level name in the organization's selected severity set. Products, releases,
/// and employees are ids. <see cref="Status"/> is a status alias. The pass creates the incident open, then
/// walks the alias on that same instance. Who submitted it is the importer's linked employee, not a column.
/// </para>
/// </remarks>
public sealed record ImportIncidentDto(
    string? ImportId,
    string Title,
    string? Symptoms,
    string? RootCause,
    string? Remediation,
    string Severity,
    ProductStatusAlias Status,
    Guid? CommanderEmployeeId,
    Guid? ReportedByEmployeeId,
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<Guid> ReleaseIds,
    Instant? NoticedAt,
    Instant? StartedAt,
    Instant? RecoveredAt,
    string? Links = null);

/// <summary>
/// Shape rules for an incident row. The status walk and the reference checks belong to the import pass.
/// </summary>
public sealed class ImportIncidentDtoValidator : AbstractValidator<ImportIncidentDto>
{
    /// <summary>The aliases an incident row may name.</summary>
    public static readonly ProductStatusAlias[] Statuses =
    [
        ProductStatusAlias.Open,
        ProductStatusAlias.Responding,
        ProductStatusAlias.Resolved,
        ProductStatusAlias.Closed,
        ProductStatusAlias.Canceled,
    ];

    public ImportIncidentDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Title)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(r => r.Symptoms)
            .MaximumLength(4096);

        RuleFor(r => r.RootCause)
            .MaximumLength(4096);

        RuleFor(r => r.Remediation)
            .MaximumLength(4096);

        RuleFor(r => r.Severity)
            .NotEmpty();

        RuleFor(r => r.Status)
            .Must(status => Statuses.Contains(status))
            .WithMessage("Status must be 'Open', 'Responding', 'Resolved', 'Closed' or 'Canceled'.");
    }
}
