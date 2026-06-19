namespace Wayd.Common.Application.Identity.Users;

public sealed record StageProviderMigrationCommand(string UserId, string TargetProviderId);

public sealed class StageProviderMigrationCommandValidator : CustomValidator<StageProviderMigrationCommand>
{
    public StageProviderMigrationCommandValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty();

        RuleFor(c => c.TargetProviderId)
            .NotEmpty()
            .MaximumLength(50);
    }
}
