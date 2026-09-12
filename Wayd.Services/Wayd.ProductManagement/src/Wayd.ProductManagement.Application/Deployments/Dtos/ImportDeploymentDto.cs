namespace Wayd.ProductManagement.Application.Deployments.Dtos;

/// <summary>
/// How an imported deployment ended, where it has.
/// </summary>
/// <remarks>
/// Its own enum rather than <c>ProductStatusAlias</c>, which also names version and package outcomes a
/// deployment can never hold. The statuses themselves are resolved from the deployment workflow by alias,
/// so a row never names a status.
/// </remarks>
public enum ImportDeploymentOutcome
{
    Succeeded = 1,
    Failed = 2,
    RolledBack = 3,
}

/// <summary>
/// A single deployment row.
/// <para>
/// A deployment carries either a version or a package, never both and never neither — where a package
/// exists it is the unit, so one pipeline run counts once. Both are referenced by id, because neither a
/// version number nor a package version is uniquely indexed. The environment is referenced by name,
/// which is unique across the organization. A build number is never resolved to a version here: which
/// version <c>4.8.2.005</c> belongs to is a rule that belongs to adapter configuration, so a row says
/// which version it deployed and carries the build as its artifact.
/// </para>
/// <para>
/// There is no status column. A row with no <see cref="Outcome"/> is still in flight; one with an
/// outcome is walked through the same transitions a person would record — started, then succeeded,
/// failed or rolled back — with its real timestamps, so the status history matches a hand-entered
/// deployment. A rollback is reached through success first, because the domain only permits rolling
/// back a deployment that reached its environment: <see cref="CompletedAt"/> is when it succeeded and
/// <see cref="RolledBackAt"/> when it was reverted.
/// </para>
/// </summary>
public sealed record ImportDeploymentDto(
    Guid? VersionId,
    Guid? PackageId,
    string EnvironmentName,
    string? ArtifactId,
    Instant StartedAt,
    ImportDeploymentOutcome? Outcome,
    Instant? CompletedAt,
    Instant? RolledBackAt,
    string? Reason);

public sealed class ImportDeploymentDtoValidator : AbstractValidator<ImportDeploymentDto>
{
    public ImportDeploymentDtoValidator()
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

        RuleFor(d => d.Outcome)
            .IsInEnum();

        // The domain keeps the ordering rules; these are the shape rules — which timestamps an outcome
        // needs — so a row that could never apply is refused before it is queued.
        RuleFor(d => d.CompletedAt)
            .NotNull()
                .When(d => d.Outcome is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage("A deployment with an outcome needs the time it completed.")
            .Null()
                .When(d => d.Outcome is null, ApplyConditionTo.CurrentValidator)
                .WithMessage("A deployment still in flight cannot have a completion time.");

        RuleFor(d => d.RolledBackAt)
            .NotNull()
                .When(d => d.Outcome == ImportDeploymentOutcome.RolledBack, ApplyConditionTo.CurrentValidator)
                .WithMessage("A rolled-back deployment needs the time it was rolled back.")
            .Null()
                .When(d => d.Outcome != ImportDeploymentOutcome.RolledBack, ApplyConditionTo.CurrentValidator)
                .WithMessage("Only a rolled-back deployment has a rollback time.");

        RuleFor(d => d.Reason)
            .MaximumLength(1024);
    }
}
