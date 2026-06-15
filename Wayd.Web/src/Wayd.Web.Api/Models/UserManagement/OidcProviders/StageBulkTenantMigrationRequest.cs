using Wayd.Common.Application.Identity.Users;

namespace Wayd.Web.Api.Models.UserManagement.OidcProviders;

/// <summary>
/// Bulk tenant migration payload. The provider id comes from the route; this body
/// carries the source/target tenants and the selected users.
/// </summary>
public sealed record StageBulkTenantMigrationRequest(
    string SourceTenantId,
    string TargetTenantId,
    IReadOnlyList<string> UserIds);

public sealed class StageBulkTenantMigrationRequestValidator : CustomValidator<StageBulkTenantMigrationRequest>
{
    public StageBulkTenantMigrationRequestValidator()
    {
        // Mirrors StageBulkTenantMigrationCommandValidator. The controller calls
        // IUserService directly (not through MediatR), so duplicating the rules here
        // produces 422s with field-level errors before reaching the service.
        RuleFor(r => r.SourceTenantId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
                .WithMessage("Source tenant id must be a valid GUID.");

        RuleFor(r => r.TargetTenantId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
                .WithMessage("Target tenant id must be a valid GUID.");

        RuleFor(r => r)
            .Must(r => !string.Equals(r.SourceTenantId, r.TargetTenantId, StringComparison.OrdinalIgnoreCase))
                .WithMessage("Target tenant must differ from the source tenant.")
            .WithName(nameof(StageBulkTenantMigrationRequest.TargetTenantId));

        RuleFor(r => r.UserIds)
            .NotEmpty()
                .WithMessage("At least one user must be selected.")
            .Must(ids => ids.Count <= StageBulkTenantMigrationCommandValidator.MaxUserIds)
                .WithMessage($"At most {StageBulkTenantMigrationCommandValidator.MaxUserIds} users can be migrated in one action.");
    }
}
