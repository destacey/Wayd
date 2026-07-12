using Wayd.Common.Domain.Identity;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Tests.Shared.Data;

public sealed class PersonalAccessTokenFaker : PrivateConstructorFaker<PersonalAccessToken>
{
    public PersonalAccessTokenFaker(Instant? timestamp = null)
    {
        var now = timestamp ?? SystemClock.Instance.GetCurrentInstant();

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Lorem.Sentence(3));
        RuleFor(x => x.TokenIdentifier, f => f.Random.Hash(40).Substring(0, 8));
        RuleFor(x => x.TokenHash, f => f.Random.Hash(40));
        RuleFor(x => x.UserId, f => f.Random.Guid().ToString());
        RuleFor(x => x.EmployeeId, f => f.Random.Bool() ? f.Random.Guid() : (Guid?)null);
        RuleFor(x => x.ExpiresAt, f => now.Plus(Duration.FromDays(f.Random.Int(30, 730))));
        RuleFor(x => x.Scopes, f => f.Random.Bool() ? null : $"[\"Permissions.{f.PickRandom("WorkItems", "Teams", "Projects")}.{f.PickRandom("View", "Create", "Update")}\"]");
        RuleFor(x => x.LastUsedAt, (Instant?)null);
        RuleFor(x => x.RevokedAt, (Instant?)null);
        RuleFor(x => x.RevokedBy, (string?)null);
    }
}

public static class PersonalAccessTokenFakerExtensions
{
    public static PersonalAccessTokenFaker WithId(this PersonalAccessTokenFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static PersonalAccessTokenFaker WithName(this PersonalAccessTokenFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static PersonalAccessTokenFaker WithTokenIdentifier(this PersonalAccessTokenFaker faker, string tokenIdentifier)
    {
        faker.RuleFor(x => x.TokenIdentifier, tokenIdentifier);
        return faker;
    }

    public static PersonalAccessTokenFaker WithTokenHash(this PersonalAccessTokenFaker faker, string tokenHash)
    {
        faker.RuleFor(x => x.TokenHash, tokenHash);
        return faker;
    }

    public static PersonalAccessTokenFaker WithUserId(this PersonalAccessTokenFaker faker, string userId)
    {
        faker.RuleFor(x => x.UserId, userId);
        return faker;
    }

    public static PersonalAccessTokenFaker WithEmployeeId(this PersonalAccessTokenFaker faker, Guid? employeeId)
    {
        faker.RuleFor(x => x.EmployeeId, employeeId);
        return faker;
    }

    public static PersonalAccessTokenFaker WithExpiresAt(this PersonalAccessTokenFaker faker, Instant expiresAt)
    {
        faker.RuleFor(x => x.ExpiresAt, expiresAt);
        return faker;
    }

    public static PersonalAccessTokenFaker WithScopes(this PersonalAccessTokenFaker faker, string? scopes)
    {
        faker.RuleFor(x => x.Scopes, scopes);
        return faker;
    }

    public static PersonalAccessTokenFaker WithLastUsedAt(this PersonalAccessTokenFaker faker, Instant? lastUsedAt)
    {
        faker.RuleFor(x => x.LastUsedAt, lastUsedAt);
        return faker;
    }

    public static PersonalAccessTokenFaker WithRevokedToken(this PersonalAccessTokenFaker faker, string revokedBy, Instant? revokedAt = null)
    {
        var now = revokedAt ?? SystemClock.Instance.GetCurrentInstant();

        faker.RuleFor(x => x.RevokedAt, now);
        faker.RuleFor(x => x.RevokedBy, revokedBy);
        return faker;
    }

    public static PersonalAccessTokenFaker AsRevoked(this PersonalAccessTokenFaker faker, string? revokedBy = null, Instant? revokedAt = null)
    {
        var now = revokedAt ?? SystemClock.Instance.GetCurrentInstant();
        var revokedById = revokedBy ?? Guid.NewGuid().ToString();

        faker.RuleFor(x => x.RevokedAt, now);
        faker.RuleFor(x => x.RevokedBy, revokedById);
        return faker;
    }

    public static PersonalAccessTokenFaker AsUsed(this PersonalAccessTokenFaker faker, Instant? lastUsedAt = null)
    {
        var usedAt = lastUsedAt ?? SystemClock.Instance.GetCurrentInstant();

        faker.RuleFor(x => x.LastUsedAt, usedAt);

        return faker;
    }

    public static PersonalAccessTokenFaker AsExpired(this PersonalAccessTokenFaker faker, Instant? timestamp = null)
    {
        var now = timestamp ?? SystemClock.Instance.GetCurrentInstant();
        var expiresAt = now.Minus(Duration.FromDays(1));

        faker.WithExpiresAt(expiresAt);

        return faker;
    }

    public static PersonalAccessTokenFaker AsActive(this PersonalAccessTokenFaker faker)
    {
        var now = SystemClock.Instance.GetCurrentInstant();


        faker.WithExpiresAt(now.Plus(Duration.FromDays(30)));

        faker.RuleFor(x => x.RevokedAt, (Instant?)null);
        faker.RuleFor(x => x.RevokedBy, (string?)null);

        return faker;
    }
}
