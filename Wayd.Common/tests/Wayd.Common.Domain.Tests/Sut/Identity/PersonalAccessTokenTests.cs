using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Identity;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.Common.Domain.Tests.Sut.Identity;

public sealed class PersonalAccessTokenTests
{
    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString());

    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Instant _now;
    private readonly PersonalAccessTokenFaker _tokenFaker;

    public PersonalAccessTokenTests()
    {
        _now = DateTime.UtcNow.ToInstant();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(_now));
        _tokenFaker = new PersonalAccessTokenFaker(_now);
    }

    [Fact]
    public void Create_ShouldReturnSuccess_WhenValidData()
    {
        // Arrange
        var fakePat = _tokenFaker.Generate();

        // Act
        var result = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, fakePat.TokenHash, fakePat.UserId, fakePat.ExpiresAt, fakePat.Scopes, Actor, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var token = result.Value;
        token.Name.Should().Be(fakePat.Name);
        token.TokenIdentifier.Should().Be(fakePat.TokenIdentifier);
        token.TokenHash.Should().Be(fakePat.TokenHash);
        token.UserId.Should().Be(fakePat.UserId);
        token.ExpiresAt.Should().Be(fakePat.ExpiresAt);
        token.Scopes.Should().Be(fakePat.Scopes);
        token.IsActiveAt(_now).Should().BeTrue();
        token.IsExpiredAt(_now).Should().BeFalse();
        token.IsRevoked.Should().BeFalse();
        token.LastUsedAt.Should().BeNull();
        token.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldReturnFailure_WhenExpirationDateIsInPast()
    {
        // Arrange
        var fakePat = _tokenFaker.AsExpired(_now.Minus(Duration.FromDays(1))).Generate();

        // Act
        var result = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, fakePat.TokenHash, fakePat.UserId, fakePat.ExpiresAt, fakePat.Scopes, Actor, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("future");
    }

    [Fact]
    public void Create_ShouldReturnFailure_WhenNameIsEmpty()
    {
        // Arrange
        var fakePat = _tokenFaker.Generate();

        // Act
        var result = PersonalAccessToken.Create(string.Empty, fakePat.TokenIdentifier, fakePat.TokenHash, fakePat.UserId, fakePat.ExpiresAt, fakePat.Scopes, Actor, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldReturnFailure_WhenTokenHashIsEmpty()
    {
        // Arrange
        var fakePat = _tokenFaker.Generate();

        // Act
        var result = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, string.Empty, fakePat.UserId, fakePat.ExpiresAt, fakePat.Scopes, Actor, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldReturnFailure_WhenUserIdIsEmpty()
    {
        // Arrange
        var fakePat = _tokenFaker.Generate();

        // Act
        var result = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, fakePat.TokenHash, string.Empty, fakePat.ExpiresAt, fakePat.Scopes, Actor, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ValidateForUse_ShouldReturnSuccess_WhenTokenIsActiveAndNotExpired()
    {
        // Arrange
        var token = _tokenFaker.Generate();

        // Act
        var result = token.ValidateForUse(_now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateForUse_ShouldReturnFailure_WhenTokenIsExpired()
    {
        // Arrange
        var expiresAt = _now.Plus(Duration.FromDays(1));
        var token = PersonalAccessToken.Create("Test", "hash1234", "hash1234567890", "user1", expiresAt, null, Actor, _now).Value;
        var futureTime = _now.Plus(Duration.FromDays(2));

        // Act
        var result = token.ValidateForUse(futureTime);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public void ValidateForUse_ShouldReturnFailure_WhenTokenIsRevoked()
    {
        // Arrange
        var revokedBy = Guid.NewGuid().ToString();
        var token = _tokenFaker.WithRevokedToken(revokedBy, _now).Generate();

        // Act
        var result = token.ValidateForUse(_now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("revoked");
    }

    [Fact]
    public void Revoke_ShouldSetRevokedAtAndRevokedBy()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var revokedBy = Guid.NewGuid().ToString();
        var revokeTime = _now.Plus(Duration.FromDays(1));

        // Act
        var result = token.Revoke(revokedBy, Actor, revokeTime);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.RevokedAt.Should().Be(revokeTime);
        token.RevokedBy.Should().Be(revokedBy);
        token.IsRevoked.Should().BeTrue();
        token.IsActiveAt(revokeTime).Should().BeFalse();
    }

    [Fact]
    public void Revoke_ShouldReturnFailure_WhenAlreadyRevoked()
    {
        // Arrange
        var revokedBy = Guid.NewGuid().ToString();
        var token = _tokenFaker.WithRevokedToken(revokedBy, _now).Generate();

        // Act
        var result = token.Revoke(Guid.NewGuid().ToString(), Actor, _now.Plus(Duration.FromDays(1)));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already revoked");
    }

    [Fact]
    public void UpdateName_ShouldUpdateNameSuccessfully()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var newName = "New Name";

        // Act
        var result = token.UpdateName(newName, Actor, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.Name.Should().Be(newName);
    }

    [Fact]
    public void UpdateName_ShouldReturnFailure_WhenTokenIsRevoked()
    {
        // Arrange
        var revokedBy = Guid.NewGuid().ToString();
        var token = _tokenFaker.WithRevokedToken(revokedBy, _now).Generate();

        // Act
        var result = token.UpdateName("New Name", Actor, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("revoked");
    }

    [Fact]
    public void UpdateExpiresAt_ShouldUpdateExpirationSuccessfully()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var newExpiresAt = _now.Plus(Duration.FromDays(180));

        // Act
        var result = token.UpdateExpiresAt(newExpiresAt, Actor, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.ExpiresAt.Should().Be(newExpiresAt);
    }

    [Fact]
    public void UpdateExpiresAt_ShouldReturnFailure_WhenTokenIsRevoked()
    {
        // Arrange
        var revokedBy = Guid.NewGuid().ToString();
        var token = _tokenFaker.WithRevokedToken(revokedBy, _now).Generate();
        var newExpiresAt = _now.Plus(Duration.FromDays(180));

        // Act
        var result = token.UpdateExpiresAt(newExpiresAt, Actor, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("revoked");
    }

    [Fact]
    public void UpdateExpiresAt_ShouldReturnFailure_WhenNewExpirationIsInPast()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var pastExpiresAt = _now.Minus(Duration.FromDays(1));

        // Act
        var result = token.UpdateExpiresAt(pastExpiresAt, Actor, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("future");
    }

    [Fact]
    public void UpdateExpiresAt_ShouldReturnFailure_WhenNewExpirationIsNow()
    {
        // Arrange
        var token = _tokenFaker.Generate();

        // Act
        var result = token.UpdateExpiresAt(_now, Actor, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("future");
    }

    [Fact]
    public void IsExpiredAt_ShouldReturnTrue_WhenTimestampIsAfterExpiration()
    {
        // Arrange
        var expiresAt = _now.Plus(Duration.FromDays(1));
        var token = PersonalAccessToken.Create("Test", "hash1234", "hash1234567890", "user1", expiresAt, null, Actor, _now).Value;

        // Act & Assert
        token.IsExpiredAt(_now).Should().BeFalse();

        var futureTime = expiresAt.Plus(Duration.FromMinutes(1));

        token.IsExpiredAt(futureTime).Should().BeTrue();
    }

    [Fact]
    public void IsActiveAt_ShouldReturnFalse_WhenTokenIsExpiredOrRevoked()
    {
        // Arrange - Revoked token
        var revokedBy = Guid.NewGuid().ToString();
        var revokedToken = _tokenFaker.WithRevokedToken(revokedBy, _now).Generate();

        // Assert
        revokedToken.IsActiveAt(_now).Should().BeFalse();
    }

    [Fact]
    public void Create_WithNullScopes_ShouldSucceed()
    {
        // Arrange
        var name = "Test Token";
        var tokenHash = "hash12345678";
        var tokenIdentifier = "hash1234";
        var userId = "user123";
        var expiresAt = _now.Plus(Duration.FromDays(365));

        // Act
        var result = PersonalAccessToken.Create(name, tokenIdentifier, tokenHash, userId, expiresAt, null, Actor, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Scopes.Should().BeNull();
    }

    [Fact]
    public void Create_RaisesCreatedEvent_WithoutTokenMaterial()
    {
        // Arrange
        var fakePat = _tokenFaker.Generate();

        // Act
        var token = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, fakePat.TokenHash, fakePat.UserId, fakePat.ExpiresAt, fakePat.Scopes, Actor, _now).Value;

        // Assert
        var created = token.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PersonalAccessTokenCreatedEvent>().Subject;
        created.Id.Should().Be(token.Id);
        created.UserId.Should().Be(fakePat.UserId);
        created.Name.Should().Be(token.Name);
        created.ExpiresAt.Should().Be(fakePat.ExpiresAt);
        created.Actor.Should().Be(Actor);
        created.Timestamp.Should().Be(_now);
        created.GetType().GetProperties().Select(p => p.Name).Should().NotContain(["TokenHash", "TokenIdentifier"]);
    }

    [Fact]
    public void Revoke_RaisesRevokedEvent()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var revokeTime = _now.Plus(Duration.FromHours(1));

        // Act
        token.Revoke(Actor.UserId!, Actor, revokeTime);

        // Assert
        var revoked = token.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PersonalAccessTokenRevokedEvent>().Subject;
        revoked.Id.Should().Be(token.Id);
        revoked.UserId.Should().Be(token.UserId);
        revoked.Actor.Should().Be(Actor);
        revoked.Timestamp.Should().Be(revokeTime);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_RaisesNothing()
    {
        // Arrange
        var token = _tokenFaker.WithRevokedToken(Guid.NewGuid().ToString(), _now).Generate();

        // Act
        token.Revoke(Actor.UserId!, Actor, _now);

        // Assert
        token.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateName_RaisesRenamedEvent_WithBothEnds()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var previousName = token.Name;

        // Act
        token.UpdateName("  Build agent  ", Actor, _now);

        // Assert
        var renamed = token.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PersonalAccessTokenRenamedEvent>().Subject;
        renamed.PreviousName.Should().Be(previousName);
        renamed.Name.Should().Be("Build agent");
        renamed.UserId.Should().Be(token.UserId);
    }

    [Fact]
    public void UpdateName_WhenOnlyWhitespaceDiffers_RaisesNothing()
    {
        // Arrange
        var token = _tokenFaker.Generate();

        // Act
        token.UpdateName($" {token.Name} ", Actor, _now);

        // Assert
        token.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateExpiresAt_RaisesExpirationChangedEvent_WithBothEnds()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var previousExpiresAt = token.ExpiresAt;
        var newExpiresAt = previousExpiresAt.Plus(Duration.FromDays(30));

        // Act
        token.UpdateExpiresAt(newExpiresAt, Actor, _now);

        // Assert
        var changed = token.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PersonalAccessTokenExpirationChangedEvent>().Subject;
        changed.PreviousExpiresAt.Should().Be(previousExpiresAt);
        changed.ExpiresAt.Should().Be(newExpiresAt);
    }

    [Fact]
    public void UpdateExpiresAt_WhenUnchanged_RaisesNothing()
    {
        // Arrange
        var token = _tokenFaker.Generate();

        // Act
        var result = token.UpdateExpiresAt(token.ExpiresAt, Actor, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_RaisesDeletedEvent_WithName()
    {
        // Arrange
        var token = _tokenFaker.Generate();

        // Act
        token.Delete(Actor, _now);

        // Assert
        var deleted = token.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PersonalAccessTokenDeletedEvent>().Subject;
        deleted.Id.Should().Be(token.Id);
        deleted.UserId.Should().Be(token.UserId);
        deleted.Name.Should().Be(token.Name);
    }
}
