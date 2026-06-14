using Wayd.Common.Domain.Identity;

namespace Wayd.Web.Api.Models.UserManagement.OidcProviders;

public sealed record CreateOidcProviderRequest : IOidcRegistrationPolicyRequest
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public OidcProviderType ProviderType { get; set; }
    public required string Authority { get; set; }
    public required string ClientId { get; set; }
    public required string Audience { get; set; }
    public IReadOnlyList<string> Scopes { get; set; } = [];
    public IReadOnlyList<string>? AllowedTenantIds { get; set; }
    public int ClockSkewSeconds { get; set; }
    public bool IsEnabled { get; set; }

    // Registration policy is a required part of the contract — callers state it
    // explicitly rather than inheriting a default. The dependent fields are only
    // meaningful when AllowAutoRegistration is true: RequireEmployeeRecord is the
    // gate (null when disabled) and DefaultRoleId is required when enabled.
    public bool AllowAutoRegistration { get; set; }
    public bool? RequireEmployeeRecord { get; set; }
    public string? DefaultRoleId { get; set; }
}

public sealed record UpdateOidcProviderRequest : IOidcRegistrationPolicyRequest
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Authority { get; set; }
    public required string ClientId { get; set; }
    public required string Audience { get; set; }
    public IReadOnlyList<string> Scopes { get; set; } = [];
    public IReadOnlyList<string>? AllowedTenantIds { get; set; }
    public int ClockSkewSeconds { get; set; }
    public bool IsEnabled { get; set; }

    // See CreateOidcProviderRequest — the registration policy is required and
    // RequireEmployeeRecord is null exactly when auto-registration is disabled.
    public bool AllowAutoRegistration { get; set; }
    public bool? RequireEmployeeRecord { get; set; }
    public string? DefaultRoleId { get; set; }
}

public static class OidcProviderRequestMessages
{
    public const string EmployeeGateRequired =
        "RequireEmployeeRecord is required when auto-registration is enabled.";
    public const string EmployeeGateForbidden =
        "RequireEmployeeRecord must be omitted when auto-registration is disabled.";
    public const string DefaultRoleRequired =
        "A default role is required when auto-registration is enabled.";
    public const string DefaultRoleForbidden =
        "A default role must be omitted when auto-registration is disabled.";
    public const string AuthorityHttps = "Authority must be an absolute HTTPS URL.";

    public static bool BeHttpsAbsoluteUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Structural validation for the create request — guarantees a well-formed shape
/// at the API boundary. The command validator in the MediatR pipeline still owns
/// the checks that need services (default-role existence, name uniqueness).
/// </summary>
/// <remarks>
/// The registration-policy invariant is expressed with model-level <c>Must</c>
/// predicates rather than property-level <c>NotNull</c>/<c>NotEmpty</c> rules.
/// The FluentValidation → NSwag schema bridge translates the latter into schema
/// <c>required</c> + non-nullable, which would force the generated TS client to
/// type the nullable <c>RequireEmployeeRecord</c>/<c>DefaultRoleId</c> as required
/// (the conditional <c>When</c> is invisible to the schema). Model-level predicates
/// validate identically at runtime without leaking into the schema.
/// </remarks>
public sealed class CreateOidcProviderRequestValidator : CustomValidator<CreateOidcProviderRequest>
{
    public CreateOidcProviderRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Authority).NotEmpty().MaximumLength(500)
            .Must(OidcProviderRequestMessages.BeHttpsAbsoluteUrl)
            .WithMessage(OidcProviderRequestMessages.AuthorityHttps);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Audience).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ClockSkewSeconds).InclusiveBetween(0, 600);

        // Entra requires at least one allowed tenant — without it every login is
        // rejected at runtime.
        RuleFor(x => x.AllowedTenantIds)
            .Must(ids => ids != null && ids.Any(t => !string.IsNullOrWhiteSpace(t)))
            .When(x => x.ProviderType == OidcProviderType.MicrosoftEntraId)
            .WithMessage("Microsoft Entra ID providers require at least one AllowedTenantId.");

        OidcProviderRequestRules.AddRegistrationPolicyRules(this);
    }
}

/// <summary>
/// Structural validation for the update request. Mirrors the create rules minus
/// the immutable Name/ProviderType fields; the command validator owns the
/// service-backed checks. See <see cref="CreateOidcProviderRequestValidator"/>
/// for why the registration-policy rules are model-level predicates.
/// </summary>
public sealed class UpdateOidcProviderRequestValidator : CustomValidator<UpdateOidcProviderRequest>
{
    public UpdateOidcProviderRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Authority).NotEmpty().MaximumLength(500)
            .Must(OidcProviderRequestMessages.BeHttpsAbsoluteUrl)
            .WithMessage(OidcProviderRequestMessages.AuthorityHttps);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Audience).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ClockSkewSeconds).InclusiveBetween(0, 600);

        OidcProviderRequestRules.AddRegistrationPolicyRules(this);
    }
}

/// <summary>
/// Shared registration-policy rules for the create/update request validators.
/// Both requests expose the same flat policy fields, so the discriminated-shape
/// invariant is identical. The rules target the model (not the nullable
/// properties directly) so the FluentValidation → NSwag schema bridge leaves
/// <c>RequireEmployeeRecord</c>/<c>DefaultRoleId</c> nullable and optional.
/// </summary>
internal static class OidcProviderRequestRules
{
    public static void AddRegistrationPolicyRules<T>(AbstractValidator<T> validator)
        where T : IOidcRegistrationPolicyRequest
    {
        // Enabled: the employee-record gate must be supplied and a default role is
        // required. Disabled: both dependent fields must be omitted — sending them
        // is a contradiction the domain would reject, so fail fast with a 400.
        validator.RuleFor(x => x)
            .Must(x => x.RequireEmployeeRecord is not null)
            .When(x => x.AllowAutoRegistration)
            .WithName(nameof(IOidcRegistrationPolicyRequest.RequireEmployeeRecord))
            .WithMessage(OidcProviderRequestMessages.EmployeeGateRequired);
        validator.RuleFor(x => x)
            .Must(x => x.RequireEmployeeRecord is null)
            .When(x => !x.AllowAutoRegistration)
            .WithName(nameof(IOidcRegistrationPolicyRequest.RequireEmployeeRecord))
            .WithMessage(OidcProviderRequestMessages.EmployeeGateForbidden);
        validator.RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.DefaultRoleId))
            .When(x => x.AllowAutoRegistration)
            .WithName(nameof(IOidcRegistrationPolicyRequest.DefaultRoleId))
            .WithMessage(OidcProviderRequestMessages.DefaultRoleRequired);
        validator.RuleFor(x => x)
            .Must(x => string.IsNullOrWhiteSpace(x.DefaultRoleId))
            .When(x => !x.AllowAutoRegistration)
            .WithName(nameof(IOidcRegistrationPolicyRequest.DefaultRoleId))
            .WithMessage(OidcProviderRequestMessages.DefaultRoleForbidden);
    }
}

/// <summary>
/// The flat registration-policy fields shared by the create and update requests.
/// Lets one set of cross-field rules validate both request types.
/// </summary>
internal interface IOidcRegistrationPolicyRequest
{
    bool AllowAutoRegistration { get; }
    bool? RequireEmployeeRecord { get; }
    string? DefaultRoleId { get; }
}
