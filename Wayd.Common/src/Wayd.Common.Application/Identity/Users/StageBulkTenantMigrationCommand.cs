namespace Wayd.Common.Application.Identity.Users;

/// <summary>
/// Stages an Entra tenant migration for multiple users at once, initiated from the
/// provider page. Each listed user is moved from <paramref name="SourceTenantId"/>
/// to <paramref name="TargetTenantId"/>; the rebind for each completes on that user's
/// next sign-in from the target tenant (same deferred mechanism as the per-user path).
/// </summary>
public sealed record StageBulkTenantMigrationCommand(
    Guid ProviderId,
    string SourceTenantId,
    string TargetTenantId,
    IReadOnlyList<string> UserIds);

/// <summary>
/// Per-user outcome of a bulk staging run. <see cref="StagedUserIds"/> are the users
/// whose <c>PendingMigrationTenantId</c> was set; <see cref="Skipped"/> carries the
/// ones that no longer qualified (re-validated server-side, so the client's list may
/// be stale) with a human-readable reason.
/// </summary>
public sealed record BulkTenantMigrationResult(
    IReadOnlyList<string> StagedUserIds,
    IReadOnlyList<SkippedUser> Skipped)
{
    public int StagedCount => StagedUserIds.Count;
}

public sealed record SkippedUser(string UserId, string Reason);

public sealed class StageBulkTenantMigrationCommandValidator : CustomValidator<StageBulkTenantMigrationCommand>
{
    // Bound the transaction. 500 is a generous cohort for a single migration batch;
    // the frontend enforces the same cap before submit so this is a backstop.
    public const int MaxUserIds = 500;

    public StageBulkTenantMigrationCommandValidator()
    {
        RuleFor(c => c.ProviderId)
            .NotEmpty();

        // Entra tenant ids are always GUIDs — gate strictly so a typo'd tid can't sit
        // on a user forever waiting for a login that can never match.
        RuleFor(c => c.SourceTenantId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
                .WithMessage("Source tenant id must be a valid GUID.");

        RuleFor(c => c.TargetTenantId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
                .WithMessage("Target tenant id must be a valid GUID.");

        // Migrating to the same tenant is a no-op that would only ever skip every user.
        RuleFor(c => c)
            .Must(c => !string.Equals(c.SourceTenantId, c.TargetTenantId, StringComparison.OrdinalIgnoreCase))
                .WithMessage("Target tenant must differ from the source tenant.")
            .WithName(nameof(StageBulkTenantMigrationCommand.TargetTenantId));

        RuleFor(c => c.UserIds)
            .NotEmpty()
                .WithMessage("At least one user must be selected.")
            .Must(ids => ids.Count <= MaxUserIds)
                .WithMessage($"At most {MaxUserIds} users can be migrated in one action.");
    }
}
