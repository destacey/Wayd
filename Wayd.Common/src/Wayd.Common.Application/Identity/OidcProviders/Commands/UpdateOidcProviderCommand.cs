using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Application.Identity.OidcProviders.Commands;

/// <summary>
/// Updates an existing OIDC provider. Name and ProviderType are intentionally
/// NOT in the command shape — both are immutable post-creation. Changing them
/// would orphan or silently re-route existing <c>UserIdentity</c> rows that
/// reference this provider.
/// </summary>
public sealed record UpdateOidcProviderCommand(
    Guid Id,
    string DisplayName,
    string Authority,
    string ClientId,
    string Audience,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string>? AllowedTenantIds,
    int ClockSkewSeconds,
    bool IsEnabled,
    bool AllowAutoRegistration,
    bool? RequireEmployeeRecord,
    string? DefaultRoleId) : ICommand;

public sealed class UpdateOidcProviderCommandValidator : CustomValidator<UpdateOidcProviderCommand>
{
    private readonly IRoleService _roleService;

    public UpdateOidcProviderCommandValidator(IRoleService roleService)
    {
        _roleService = roleService;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Authority).NotEmpty().MaximumLength(500)
            .Must(BeHttpsAbsoluteUrl).WithMessage("Authority must be an absolute HTTPS URL.");
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Audience).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ClockSkewSeconds).InclusiveBetween(0, 600);
        // The ProviderType-specific tenant-list rule lives in the handler — we
        // need to read the persisted ProviderType to know which branch applies,
        // and FluentValidation can't easily fetch the row here without coupling
        // to IWaydDbContext just for one MustAsync.

        // When auto-registration is enabled a default role is required and must
        // reference an existing role; when disabled it's ignored.
        RuleFor(x => x.DefaultRoleId)
            .NotEmpty().WithMessage("A default role is required when auto-registration is enabled.")
            .DependentRules(() =>
                RuleFor(x => x.DefaultRoleId)
                    .MustAsync(BeAnExistingRole)
                    .WithMessage("The selected default role does not exist."))
            .When(x => x.AllowAutoRegistration);
    }

    private static bool BeHttpsAbsoluteUrl(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> BeAnExistingRole(string? roleId, CancellationToken cancellationToken)
    {
        return await _roleService.GetById(roleId!.Trim(), cancellationToken) is not null;
    }
}

public sealed class UpdateOidcProviderCommandHandler(
    IWaydDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IOidcProviderRegistry registry,
    ILogger<UpdateOidcProviderCommandHandler> logger)
    : ICommandHandler<UpdateOidcProviderCommand>
{
    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IOidcProviderRegistry _registry = registry;
    private readonly ILogger<UpdateOidcProviderCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateOidcProviderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var provider = await _dbContext.OidcProviders
                .SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (provider is null)
            {
                return Result.Failure("Provider not found.");
            }

            var updateResult = provider.Update(
                displayName: request.DisplayName,
                authority: request.Authority,
                clientId: request.ClientId,
                audience: request.Audience,
                scopes: request.Scopes ?? [],
                allowedTenantIds: request.AllowedTenantIds,
                clockSkewSeconds: request.ClockSkewSeconds,
                isEnabled: request.IsEnabled,
                timestamp: _dateTimeProvider.Now,
                registrationPolicy: RegistrationPolicy.FromFlat(
                    request.AllowAutoRegistration,
                    request.RequireEmployeeRecord,
                    request.DefaultRoleId));

            if (updateResult.IsFailure)
            {
                return updateResult;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // The provider name is the cache key. Invalidate by name so the
            // next exchange/listing reflects the new config immediately.
            _registry.Invalidate(provider.Name);

            _logger.LogInformation("Updated OIDC provider {ProviderId} ({Name}).", provider.Id, provider.Name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update OIDC provider {ProviderId}.", request.Id);
            return Result.Failure("Failed to update OIDC provider.");
        }
    }
}
