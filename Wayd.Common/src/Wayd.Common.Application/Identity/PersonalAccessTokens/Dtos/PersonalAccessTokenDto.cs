using System.Linq.Expressions;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Application.Identity.PersonalAccessTokens.Dtos;

/// <summary>
/// A personal access token's metadata and status. The token value itself is never
/// included; it is returned only once, when the token is created.
/// </summary>
public sealed record PersonalAccessTokenDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public Instant ExpiresAt { get; init; }
    public Instant? LastUsedAt { get; init; }
    public Instant? RevokedAt { get; init; }
    public bool IsActive { get; init; }
    public bool IsExpired { get; init; }
    public bool IsRevoked { get; init; }

    /// <summary>How much of its owner's access the token may use.</summary>
    public TokenScope Scope { get; init; }

    /// <summary>
    /// EF-translatable projection from a <see cref="PersonalAccessToken"/> to this DTO,
    /// computing the time-relative status flags against <paramref name="now"/>.
    /// The status booleans are not stored, and the domain exposes them as
    /// timestamp methods (<see cref="PersonalAccessToken.IsExpiredAt"/> /
    /// <see cref="PersonalAccessToken.IsActiveAt"/>) rather than properties, so the
    /// projection reproduces that logic inline where it can be translated to SQL.
    /// </summary>
    public static Expression<Func<PersonalAccessToken, PersonalAccessTokenDto>> Projection(Instant now) =>
        token => new PersonalAccessTokenDto
        {
            Id = token.Id,
            Name = token.Name,
            ExpiresAt = token.ExpiresAt,
            LastUsedAt = token.LastUsedAt,
            RevokedAt = token.RevokedAt,
            IsRevoked = token.RevokedAt.HasValue,
            IsExpired = token.ExpiresAt <= now,
            IsActive = !(token.ExpiresAt <= now) && !token.RevokedAt.HasValue,
            // Spelled out rather than read from PersonalAccessToken.Scope, which EF cannot translate. A token from
            // before scopes has none and keeps its owner's full access.
            Scope = token.Scopes == TokenScopes.Read ? TokenScope.Read : TokenScope.Write,
        };
}
