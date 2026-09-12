using NodaTime.Text;
using Wayd.ProductManagement.Application.Deployments.Dtos;

namespace Wayd.Web.Api.Models.ProductManagement.Deployments;

/// <summary>
/// A single CSV row for the deployment import.
/// <para>
/// Exactly one of <see cref="VersionId"/> and <see cref="PackageId"/> is set, both by id. The
/// environment is named, because names are unique. A build number is never resolved to a version: the
/// row says which version it deployed and carries the build as its <see cref="ArtifactId"/>.
/// </para>
/// <para>
/// There is no status column. A row with no <see cref="Outcome"/> is still in flight; one with an
/// outcome is walked through the same transitions a person would record, with the real timestamps
/// supplied here. A rollback is recorded as a success first, so it needs both the time it completed
/// and the time it was reverted.
/// </para>
/// <para>
/// Timestamps are instants, and each must carry its offset — <c>2026-03-01T14:30:00Z</c> or
/// <c>2026-03-01T09:30:00-05:00</c>. A value with no offset is refused rather than read in the
/// server's zone, which would shift every historical deployment by whatever that zone happens to be.
/// </para>
/// </summary>
public sealed class ImportDeploymentRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The version deployed, by id. Leave empty when a package was deployed.</summary>
    public Guid? VersionId { get; set; }

    /// <summary>The package deployed, by id. Leave empty when a version was deployed.</summary>
    public Guid? PackageId { get; set; }

    /// <summary>The environment reached, by name.</summary>
    public string EnvironmentName { get; set; } = default!;

    /// <summary>The build that actually shipped — <c>4.8.2.008</c> where the version number is <c>4.8.2</c>.</summary>
    public string? ArtifactId { get; set; }

    /// <summary>When the deployment began, with its offset.</summary>
    public string StartedAt { get; set; } = default!;

    /// <summary>
    /// <c>Succeeded</c>, <c>Failed</c> or <c>RolledBack</c>, case-insensitively. Blank leaves the
    /// deployment in flight.
    /// </summary>
    public string? Outcome { get; set; }

    /// <summary>
    /// When the deployment reached its outcome, with its offset. Required with an outcome. For a
    /// rollback, when it succeeded.
    /// </summary>
    public string? CompletedAt { get; set; }

    /// <summary>When a rolled-back deployment was reverted, with its offset. Required for a rollback.</summary>
    public string? RolledBackAt { get; set; }

    /// <summary>Why it failed or was rolled back. Max 1024 chars.</summary>
    public string? Reason { get; set; }

    public ImportDeploymentDto ToImportDeploymentDto() =>
        new(VersionId,
            PackageId,
            EnvironmentName,
            ArtifactId,
            ParseInstant(StartedAt)!.Value,
            ParseOutcome(Outcome),
            ParseInstant(CompletedAt),
            ParseInstant(RolledBackAt),
            Reason);

    /// <summary>
    /// Reads an ISO-8601 timestamp that carries its offset. Null for a blank cell, and for a value the
    /// validator has already refused.
    /// </summary>
    internal static Instant? ParseInstant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parsed = OffsetDateTimePattern.ExtendedIso.Parse(value.Trim());

        return parsed.Success ? parsed.Value.ToInstant() : null;
    }

    /// <summary>
    /// Reads an outcome by name. Null for a blank cell, and for a value the validator has already refused.
    /// </summary>
    internal static ImportDeploymentOutcome? ParseOutcome(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Enum.TryParse<ImportDeploymentOutcome>(value.Trim(), ignoreCase: true, out var outcome)
        && Enum.IsDefined(outcome)
            ? outcome
            : null;
}

public sealed class ImportDeploymentRequestValidator : CustomValidator<ImportDeploymentRequest>
{
    public ImportDeploymentRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(d => d)
            .Must(d => d.VersionId is not null ^ d.PackageId is not null)
                .WithMessage("A deployment is for either a version or a package, not both and not neither.");

        RuleFor(d => d.EnvironmentName)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(d => d.ArtifactId)
            .MaximumLength(128);

        RuleFor(d => d.StartedAt)
            .NotEmpty()
            .Must(BeAnOffsetTimestamp)
                .WithMessage("StartedAt must be an ISO-8601 timestamp with an offset, such as 2026-03-01T14:30:00Z.");

        RuleFor(d => d.Outcome)
            .Must(o => string.IsNullOrWhiteSpace(o)
                || (Enum.TryParse<ImportDeploymentOutcome>(o.Trim(), ignoreCase: true, out var outcome)
                    && Enum.IsDefined(outcome)))
                .WithMessage("Outcome must be blank, 'Succeeded', 'Failed' or 'RolledBack'.");

        RuleFor(d => d.CompletedAt)
            .Must(BeAnOffsetTimestamp)
                .When(d => !string.IsNullOrWhiteSpace(d.CompletedAt), ApplyConditionTo.CurrentValidator)
                .WithMessage("CompletedAt must be an ISO-8601 timestamp with an offset, such as 2026-03-01T14:30:00Z.")
            .NotEmpty()
                .When(d => !string.IsNullOrWhiteSpace(d.Outcome), ApplyConditionTo.CurrentValidator)
                .WithMessage("A deployment with an outcome needs the time it completed.")
            .Empty()
                .When(d => string.IsNullOrWhiteSpace(d.Outcome), ApplyConditionTo.CurrentValidator)
                .WithMessage("A deployment still in flight cannot have a completion time.")
            .Must((row, completed) => IsNotBefore(completed, row.StartedAt))
                .WithMessage("The completion cannot be before the deployment started.");

        RuleFor(d => d.RolledBackAt)
            .Must(BeAnOffsetTimestamp)
                .When(d => !string.IsNullOrWhiteSpace(d.RolledBackAt), ApplyConditionTo.CurrentValidator)
                .WithMessage("RolledBackAt must be an ISO-8601 timestamp with an offset, such as 2026-03-01T14:30:00Z.")
            .NotEmpty()
                .When(d => IsRolledBack(d.Outcome), ApplyConditionTo.CurrentValidator)
                .WithMessage("A rolled-back deployment needs the time it was rolled back.")
            .Empty()
                .When(d => !IsRolledBack(d.Outcome), ApplyConditionTo.CurrentValidator)
                .WithMessage("Only a rolled-back deployment has a rollback time.")
            .Must((row, rolledBack) => IsNotBefore(rolledBack, row.CompletedAt))
                .WithMessage("The rollback cannot be before the deployment completed.");

        RuleFor(d => d.Reason)
            .MaximumLength(1024);
    }

    private static bool BeAnOffsetTimestamp(string? value) =>
        ImportDeploymentRequest.ParseInstant(value) is not null;

    private static bool IsRolledBack(string? outcome) =>
        ImportDeploymentRequest.ParseOutcome(outcome) == ImportDeploymentOutcome.RolledBack;

    /// <summary>
    /// True unless both parse and the first is earlier than the second. A value that does not parse
    /// has already been refused by its own rule, so this never reports it twice.
    /// </summary>
    private static bool IsNotBefore(string? later, string? earlier)
    {
        var laterInstant = ImportDeploymentRequest.ParseInstant(later);
        var earlierInstant = ImportDeploymentRequest.ParseInstant(earlier);

        return laterInstant is null || earlierInstant is null || laterInstant >= earlierInstant;
    }
}
