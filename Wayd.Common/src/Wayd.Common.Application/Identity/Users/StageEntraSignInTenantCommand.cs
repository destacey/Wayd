namespace Wayd.Common.Application.Identity.Users;

/// <summary>
/// Stages the tenant an Entra user with no active identity links from on their next sign-in.
/// <see cref="TenantId"/> is optional when the Entra provider allows a single tenant.
/// </summary>
public sealed record StageEntraSignInTenantCommand(string UserId, string? TenantId);

public sealed class StageEntraSignInTenantCommandValidator : CustomValidator<StageEntraSignInTenantCommand>
{
    public StageEntraSignInTenantCommandValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty();

        RuleFor(c => c.TenantId)
            .MaximumLength(100);
    }
}
