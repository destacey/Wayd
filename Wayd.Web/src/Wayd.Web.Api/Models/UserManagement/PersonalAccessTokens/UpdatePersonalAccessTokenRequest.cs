using Wayd.Common.Application.Identity.PersonalAccessTokens.Commands;
using Wayd.Common.Application.Interfaces;

namespace Wayd.Web.Api.Models.UserManagement.PersonalAccessTokens;

public sealed record UpdatePersonalAccessTokenRequest
{
    /// <summary>
    /// The user-friendly name for this token.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The expiration date for the token. Must be in the future and within 2 years from now.
    /// </summary>
    public Instant ExpiresAt { get; set; }

    public UpdatePersonalAccessTokenCommand ToUpdatePersonalAccessTokenCommand(Guid tokenId)
        => new(tokenId, Name, ExpiresAt);
}

public sealed class UpdatePersonalAccessTokenRequestValidator : CustomValidator<UpdatePersonalAccessTokenRequest>
{
    private const int MaxExpirationDays = 730; // 2 years

    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdatePersonalAccessTokenRequestValidator(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ExpiresAt)
            .Must(BeValidExpirationDate)
                .WithMessage($"Expiration date must be between 1 day and {MaxExpirationDays} days ({MaxExpirationDays / 365} years) from now.");
    }

    private bool BeValidExpirationDate(Instant expiresAt)
    {
        var now = _dateTimeProvider.Now;
        var minExpiration = now.Plus(Duration.FromDays(1));
        var maxExpiration = now.Plus(Duration.FromDays(MaxExpirationDays));

        return expiresAt >= minExpiration && expiresAt <= maxExpiration;
    }
}
