using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;
using NodaTime;

namespace Wayd.Common.Domain.Identity;

/// <summary>
/// Represents a personal access token used for API authentication.
/// </summary>
public sealed class PersonalAccessToken : BaseAuditableEntity
{
    private PersonalAccessToken() { }

    private PersonalAccessToken(
        string name,
        string tokenIdentifier,
        string tokenHash,
        string userId,
        Guid? employeeId,
        Instant expiresAt,
        Instant timestamp)
    {
        Name = name;
        TokenIdentifier = tokenIdentifier;
        TokenHash = tokenHash;
        UserId = userId;
        EmployeeId = employeeId;
        ExpiresAt = expiresAt;
        LastUsedAt = null;
        RevokedAt = null;
        RevokedBy = null;
    }

    /// <summary>
    /// Gets the user-friendly name for this token.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>
    /// Gets the token identifier (first 8 characters). Used for efficient database lookups.
    /// </summary>
    public string TokenIdentifier
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(TokenIdentifier));
    } = null!;

    /// <summary>
    /// Gets the hashed token value. The plain text token is never stored.
    /// </summary>
    public string TokenHash
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(TokenHash));
    } = null!;

    /// <summary>
    /// Gets the ID of the user who owns this token.
    /// </summary>
    public string UserId
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(UserId));
    } = null!;

    /// <summary>
    /// Gets the optional employee ID associated with this token's user.
    /// </summary>
    public Guid? EmployeeId { get; private set; }

    /// <summary>
    /// Gets the scopes/permissions this token is limited to.
    /// Null or empty means full access (all user permissions).
    /// JSON array of permission names (e.g., ["Permissions.WorkItems.View"]).
    /// </summary>
    public string? Scopes { get; private set; }

    /// <summary>
    /// Gets when this token expires.
    /// </summary>
    public Instant ExpiresAt { get; private set; }

    /// <summary>
    /// Gets when this token was last used for authentication.
    /// </summary>
    public Instant? LastUsedAt { get; private set; }

    /// <summary>
    /// Gets when this token was revoked, if applicable.
    /// </summary>
    public Instant? RevokedAt { get; private set; }

    /// <summary>
    /// Gets the ID of the user who revoked this token, if applicable.
    /// </summary>
    public string? RevokedBy { get; private set; }

    /// <summary>
    /// Indicates whether this token is currently active (not expired and not revoked).
    /// </summary>
    public bool IsActiveAt(Instant timestamp) => !IsExpiredAt(timestamp) && !IsRevoked;

    /// <summary>
    /// Indicates whether this token has expired.
    /// </summary>
    public bool IsExpiredAt(Instant timestamp) => ExpiresAt <= timestamp;

    /// <summary>
    /// Indicates whether this token has been revoked.
    /// </summary>
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>
    /// Validates that this token is usable for authentication.
    /// </summary>
    /// <param name="timestamp">The current timestamp.</param>
    /// <returns>A Result indicating success or the reason for failure.</returns>
    public Result ValidateForUse(Instant timestamp)
    {
        if (IsRevoked)
        {
            return Result.Failure("Token has been revoked.");
        }

        if (timestamp >= ExpiresAt)
        {
            return Result.Failure("Token has expired.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Updates the last used timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp when the token was used.</param>
    public void UpdateLastUsed(Instant timestamp)
    {
        LastUsedAt = timestamp;
    }

    /// <summary>
    /// Revokes this token.
    /// </summary>
    /// <param name="revokedBy">The ID of the user revoking the token.</param>
    /// <param name="timestamp">The timestamp for the revocation.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result Revoke(string revokedBy, Instant timestamp)
    {
        if (IsRevoked)
        {
            return Result.Failure("Token is already revoked.");
        }

        RevokedAt = timestamp;
        RevokedBy = revokedBy;

        AddDomainEvent(EntityDeletedEvent.WithEntity(this, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Updates the token name.
    /// </summary>
    /// <param name="name">The new name.</param>
    /// <param name="timestamp">The timestamp for the update.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result UpdateName(string name, Instant timestamp)
    {
        try
        {
            if (IsRevoked)
            {
                return Result.Failure("Cannot update a revoked token.");
            }

            Name = name;
            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Updates the token expiration date.
    /// </summary>
    /// <param name="expiresAt">The new expiration date.</param>
    /// <param name="timestamp">The timestamp for the update.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result UpdateExpiresAt(Instant expiresAt, Instant timestamp)
    {
        if (IsRevoked)
        {
            return Result.Failure("Cannot update a revoked token.");
        }

        if (expiresAt <= timestamp)
        {
            return Result.Failure("Expiration date must be in the future.");
        }

        ExpiresAt = expiresAt;
        AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Creates a new personal access token.
    /// </summary>
    /// <param name="name">User-friendly name for the token.</param>
    /// <param name="tokenIdentifier">The token identifier (first 8 characters).</param>
    /// <param name="tokenHash">The hashed token value.</param>
    /// <param name="userId">The ID of the user who owns this token.</param>
    /// <param name="employeeId">Optional employee ID.</param>
    /// <param name="expiresAt">When the token expires.</param>
    /// <param name="scopes">Optional scopes to limit token permissions.</param>
    /// <param name="timestamp">The timestamp for the creation event.</param>
    /// <returns>A Result containing the new PersonalAccessToken or an error.</returns>
    public static Result<PersonalAccessToken> Create(
        string name,
        string tokenIdentifier,
        string tokenHash,
        string userId,
        Guid? employeeId,
        Instant expiresAt,
        string? scopes,
        Instant timestamp)
    {
        try
        {
            if (expiresAt <= timestamp)
            {
                return Result.Failure<PersonalAccessToken>("Expiration date must be in the future.");
            }

            var token = new PersonalAccessToken(name, tokenIdentifier, tokenHash, userId, employeeId, expiresAt, timestamp)
            {
                Scopes = scopes
            };

            token.AddDomainEvent(EntityCreatedEvent.WithEntity(token, timestamp));

            return Result.Success(token);
        }
        catch (Exception ex)
        {
            return Result.Failure<PersonalAccessToken>(ex.Message);
        }
    }
}
