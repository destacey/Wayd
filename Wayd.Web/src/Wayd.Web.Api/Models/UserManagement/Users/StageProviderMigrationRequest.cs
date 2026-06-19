namespace Wayd.Web.Api.Models.UserManagement.Users;

public sealed record StageProviderMigrationRequest(string TargetProviderId);

public sealed class StageProviderMigrationRequestValidator : CustomValidator<StageProviderMigrationRequest>
{
    public StageProviderMigrationRequestValidator()
    {
        RuleFor(r => r.TargetProviderId)
            .NotEmpty()
            .MaximumLength(50);
    }
}
