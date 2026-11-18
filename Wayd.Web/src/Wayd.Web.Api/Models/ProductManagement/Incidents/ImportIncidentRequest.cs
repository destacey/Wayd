using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.Web.Api.Models.ProductManagement;

namespace Wayd.Web.Api.Models.ProductManagement.Incidents;

/// <summary>
/// A single CSV row for the incident import.
/// </summary>
/// <remarks>
/// Severity is a level name in the selected severity set, not an id. Products, releases, and employees
/// are ids. Status is an alias. Who submitted the incident is the importer's linked employee.
/// Timestamps are instants, and each must carry its offset (see <c>OffsetTimestamp</c>).
/// </remarks>
public sealed class ImportIncidentRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Required: a blank cell fails the row, and the row is not numbered by its
    /// position instead. Never stored on the incident.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>What happened, in a line a responder can scan. Max 256 characters.</summary>
    public string Title { get; set; } = default!;

    /// <summary>What responders are seeing, in Markdown. Max 4096 characters. Blank records none.</summary>
    public string? Symptoms { get; set; }

    /// <summary>Why it happened, in Markdown. Max 4096 characters. Blank records none.</summary>
    public string? RootCause { get; set; }

    /// <summary>What was done about it, in Markdown. Max 4096 characters. Blank records none.</summary>
    public string? Remediation { get; set; }

    /// <summary>
    /// The severity level's name in the organization's selected severity set, such as <c>SEV-1</c>.
    /// A name from a set that is not selected fails the row. Matched case-insensitively.
    /// </summary>
    public string Severity { get; set; } = default!;

    /// <summary>
    /// <c>Open</c>, <c>Responding</c>, <c>Resolved</c>, <c>Closed</c>, or <c>Canceled</c>,
    /// case-insensitively. <c>Recovered</c> is the screen name of <c>Resolved</c> and is not a value here.
    /// The incident is created open and then walked to this alias.
    /// </summary>
    public string Status { get; set; } = default!;

    /// <summary>The commander, by employee id. Blank leaves the incident without one.</summary>
    public Guid? CommanderEmployeeId { get; set; }

    /// <summary>
    /// Who reported it, by employee id. Blank uses the importer's linked employee, who is always
    /// recorded as who submitted it.
    /// </summary>
    public Guid? ReportedByEmployeeId { get; set; }

    /// <summary>
    /// The products the incident points at, as a semicolon-separated list of ids. An unknown id fails
    /// the row. Blank records none.
    /// </summary>
    public string? ProductIds { get; set; }

    /// <summary>
    /// The releases the incident points at, as a semicolon-separated list of ids. An unknown id fails
    /// the row. Blank records none.
    /// </summary>
    public string? ReleaseIds { get; set; }

    /// <summary>
    /// When the incident was noticed, with its offset. Blank records the time of the import, and a
    /// closed walk then stamps response and recovery at that same time unless those columns are filled.
    /// </summary>
    public string? NoticedAt { get; set; }

    /// <summary>
    /// When response started, with its offset. Blank leaves it empty on an open incident. A later status
    /// fills it from this cell, or from <c>RecoveredAt</c> when that is set and this is not, or from the
    /// time of the import when both are blank.
    /// </summary>
    public string? StartedAt { get; set; }

    /// <summary>When service was restored, with its offset. Blank leaves it for the status walk to fill.</summary>
    public string? RecoveredAt { get; set; }

    public ImportIncidentDto ToImportIncidentDto() =>
        new(ImportId,
            Title,
            Symptoms,
            RootCause,
            Remediation,
            Severity,
            ParseStatus(Status)!.Value,
            CommanderEmployeeId,
            ReportedByEmployeeId,
            CsvList.SplitIds(ProductIds),
            CsvList.SplitIds(ReleaseIds),
            OffsetTimestamp.Parse(NoticedAt),
            OffsetTimestamp.Parse(StartedAt),
            OffsetTimestamp.Parse(RecoveredAt));

    /// <summary>
    /// Reads a status alias by name. Null for a blank cell, for <c>Recovered</c>, and for a value the
    /// validator has already refused.
    /// </summary>
    internal static ProductStatusAlias? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Enum.TryParse<ProductStatusAlias>(value.Trim(), ignoreCase: true, out var status)
            || !Enum.IsDefined(status)
            || !ImportIncidentDtoValidator.Statuses.Contains(status))
        {
            return null;
        }

        return status;
    }
}

public sealed class ImportIncidentRequestValidator : CustomValidator<ImportIncidentRequest>
{
    public ImportIncidentRequestValidator()
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
            .NotEmpty()
            .Must(status => ImportIncidentRequest.ParseStatus(status) is not null)
            .WithMessage("Status must be 'Open', 'Responding', 'Resolved', 'Closed' or 'Canceled'.");

        RuleFor(r => r.ProductIds)
            .Must(CsvList.AreAllIds)
            .WithMessage("ProductIds must be a semicolon-separated list of ids.");

        RuleFor(r => r.ReleaseIds)
            .Must(CsvList.AreAllIds)
            .WithMessage("ReleaseIds must be a semicolon-separated list of ids.");

        RuleFor(r => r.NoticedAt)
            .Must(OffsetTimestamp.IsValid)
            .When(r => !string.IsNullOrWhiteSpace(r.NoticedAt))
            .WithMessage(OffsetTimestamp.Message(nameof(ImportIncidentRequest.NoticedAt)));

        RuleFor(r => r.StartedAt)
            .Must(OffsetTimestamp.IsValid)
            .When(r => !string.IsNullOrWhiteSpace(r.StartedAt))
            .WithMessage(OffsetTimestamp.Message(nameof(ImportIncidentRequest.StartedAt)));

        RuleFor(r => r.RecoveredAt)
            .Must(OffsetTimestamp.IsValid)
            .When(r => !string.IsNullOrWhiteSpace(r.RecoveredAt))
            .WithMessage(OffsetTimestamp.Message(nameof(ImportIncidentRequest.RecoveredAt)));
    }
}
