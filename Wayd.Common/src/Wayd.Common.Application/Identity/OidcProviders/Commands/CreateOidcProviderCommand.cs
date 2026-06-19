using Wayd.Common.Application.Identity.OidcProviders.Dtos;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Application.Identity.OidcProviders.Commands;

/// <summary>
/// Creates a new OIDC provider. <c>Name</c> is the stable key written into
/// <c>UserIdentity.Provider</c> on every login through this provider — it's
/// immutable post-creation, and uniqueness is enforced by both DB index and
/// validator.
/// </summary>
public sealed record CreateOidcProviderCommand(
    string Name,
    string DisplayName,
    OidcProviderType ProviderType,
    string Authority,
    string ClientId,
    string Audience,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string>? AllowedTenantIds,
    int ClockSkewSeconds,
    bool IsEnabled,
    bool AllowAutoRegistration,
    bool? RequireEmployeeRecord,
    string? DefaultRoleId) : ICommand<OidcProviderDto>;

public sealed class CreateOidcProviderCommandValidator : CustomValidator<CreateOidcProviderCommand>
{
    private readonly IWaydDbContext _dbContext;
    private readonly IRoleService _roleService;

    public CreateOidcProviderCommandValidator(IWaydDbContext dbContext, IRoleService roleService)
    {
        _dbContext = dbContext;
        _roleService = roleService;
        RuleLevelCascadeMode = CascadeMode.Stop;

        // Field-level shape — domain entity also enforces these, but failing
        // here produces clearer 400s than the generic Result.Failure path.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Authority).NotEmpty().MaximumLength(500)
            .Must(BeHttpsAbsoluteUrl).WithMessage("Authority must be an absolute HTTPS URL.");
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Audience).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ClockSkewSeconds).InclusiveBetween(0, 600);

        RuleFor(x => x.Name)
            .MustAsync(BeUniqueName)
            .WithMessage("A provider with this name already exists.");

        // Entra requires at least one allowed tenant — without it the validator
        // would reject every login at runtime.
        RuleFor(x => x.AllowedTenantIds)
            .Must(ids => ids != null && ids.Any(t => !string.IsNullOrWhiteSpace(t)))
            .When(x => x.ProviderType == OidcProviderType.MicrosoftEntraId)
            .WithMessage("Microsoft Entra ID providers require at least one AllowedTenantId.");

        // The "Wayd" name is reserved for the local-account identity in
        // UserIdentity. Allowing it here would let an OIDC provider impersonate
        // local accounts during identity lookup.
        RuleFor(x => x.Name)
            .Must(n => !string.Equals(n?.Trim(), "Wayd", StringComparison.OrdinalIgnoreCase))
            .WithMessage("'Wayd' is a reserved provider name.");

        // When auto-registration is enabled a default role is required and must
        // reference an existing role. Validated here so it's a clean 400 rather than
        // the entity throwing at construction time. When disabled the role is ignored.
        RuleFor(x => x.DefaultRoleId)
            .NotEmpty().WithMessage("A default role is required when auto-registration is enabled.")
            .DependentRules(() =>
                RuleFor(x => x.DefaultRoleId)
                    .MustAsync(BeAnExistingRole)
                    .WithMessage("The selected default role does not exist."))
            .When(x => x.AllowAutoRegistration);
    }

    private async Task<bool> BeAnExistingRole(string? roleId, CancellationToken cancellationToken)
    {
        return await _roleService.GetById(roleId!.Trim(), cancellationToken) is not null;
    }

    private static bool BeHttpsAbsoluteUrl(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
    {
        return !await _dbContext.OidcProviders
            .AnyAsync(p => p.Name == name, cancellationToken);
    }
}

internal sealed class CreateOidcProviderCommandHandler(
    IWaydDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IOidcProviderRegistry registry,
    ILogger<CreateOidcProviderCommandHandler> logger)
    : ICommandHandler<CreateOidcProviderCommand, OidcProviderDto>
{
    private readonly IWaydDbContext _dbContext = dbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IOidcProviderRegistry _registry = registry;
    private readonly ILogger<CreateOidcProviderCommandHandler> _logger = logger;

    public async Task<Result<OidcProviderDto>> Handle(CreateOidcProviderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = OidcProvider.Create(
                name: request.Name,
                displayName: request.DisplayName,
                providerType: request.ProviderType,
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

            if (result.IsFailure)
            {
                return Result.Failure<OidcProviderDto>(result.Error);
            }

            var provider = result.Value;
            _dbContext.OidcProviders.Add(provider);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Drop the all-enabled cache entry so the new provider shows up on
            // the next /api/auth/providers request without waiting out the TTL.
            _registry.InvalidateAll();

            _logger.LogInformation(
                "Created OIDC provider {ProviderId} ({Name}, type {ProviderType}).",
                provider.Id, provider.Name, provider.ProviderType);

            return Result.Success(ToDto(provider));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OIDC provider {Name}.", request.Name);
            return Result.Failure<OidcProviderDto>("Failed to create OIDC provider.");
        }
    }

    private static OidcProviderDto ToDto(OidcProvider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        DisplayName = p.DisplayName,
        ProviderType = p.ProviderType.ToString(),
        Authority = p.Authority,
        ClientId = p.ClientId,
        Audience = p.Audience,
        Scopes = p.Scopes,
        AllowedTenantIds = p.AllowedTenantIds,
        ClockSkewSeconds = p.ClockSkewSeconds,
        IsEnabled = p.IsEnabled,
        AllowAutoRegistration = p.RegistrationPolicy.AllowAutoRegistration,
        // Flatten the gated value for the admin form; when auto-registration is
        // off the gate has no stored value, so surface its default for display.
        RequireEmployeeRecord = p.RegistrationPolicy.RequireEmployeeRecord ?? true,
        DefaultRoleId = p.RegistrationPolicy.DefaultRoleId,
    };
}
