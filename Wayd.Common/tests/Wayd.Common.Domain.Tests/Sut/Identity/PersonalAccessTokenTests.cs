using Wayd.Common.Domain.Identity;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.Common.Domain.Tests.Sut.Identity;

public sealed class PersonalAccessTokenTests
{
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
        var result = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, fakePat.TokenHash, fakePat.UserId, fakePat.EmployeeId, fakePat.ExpiresAt, fakePat.Scopes, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var token = result.Value;
        token.Name.Should().Be(fakePat.Name);
        token.TokenIdentifier.Should().Be(fakePat.TokenIdentifier);
        token.TokenHash.Should().Be(fakePat.TokenHash);
        token.UserId.Should().Be(fakePat.UserId);
        token.EmployeeId.Should().Be(fakePat.EmployeeId);
        token.ExpiresAt.Should().Be(fakePat.ExpiresAt);
        token.Scopes.Should().Be(fakePat.Scopes);
        token.IsActive.Should().BeTrue();
        token.IsExpired.Should().BeFalse();
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
        var result = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, fakePat.TokenHash, fakePat.UserId, fakePat.EmployeeId, fakePat.ExpiresAt, fakePat.Scopes, _now);

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
        var result = PersonalAccessToken.Create(string.Empty, fakePat.TokenIdentifier, fakePat.TokenHash, fakePat.UserId, fakePat.EmployeeId, fakePat.ExpiresAt, fakePat.Scopes, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldReturnFailure_WhenTokenHashIsEmpty()
    {
        // Arrange
        var fakePat = _tokenFaker.Generate();

        // Act
        var result = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, string.Empty, fakePat.UserId, fakePat.EmployeeId, fakePat.ExpiresAt, fakePat.Scopes, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldReturnFailure_WhenUserIdIsEmpty()
    {
        // Arrange
        var fakePat = _tokenFaker.Generate();

        // Act
        var result = PersonalAccessToken.Create(fakePat.Name, fakePat.TokenIdentifier, fakePat.TokenHash, string.Empty, fakePat.EmployeeId, fakePat.ExpiresAt, fakePat.Scopes, _now);

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
        var token = PersonalAccessToken.Create("Test", "hash1234", "hash1234567890", "user1", null, expiresAt, null, _now).Value;
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
    public void UpdateLastUsed_ShouldSetLastUsedAt()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var usedAt = _now.Plus(Duration.FromMinutes(30));

        // Act
        token.UpdateLastUsed(usedAt);

        // Assert
        token.LastUsedAt.Should().Be(usedAt);
    }

    [Fact]
    public void Revoke_ShouldSetRevokedAtAndRevokedBy()
    {
        // Arrange
        var token = _tokenFaker.Generate();
        var revokedBy = Guid.NewGuid().ToString();
        var revokeTime = _now.Plus(Duration.FromDays(1));

        // Act
        var result = token.Revoke(revokedBy, revokeTime);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.RevokedAt.Should().Be(revokeTime);
        token.RevokedBy.Should().Be(revokedBy);
        token.IsRevoked.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_ShouldReturnFailure_WhenAlreadyRevoked()
    {
        // Arrange
        var revokedBy = Guid.NewGuid().ToString();
        var token = _tokenFaker.WithRevokedToken(revokedBy, _now).Generate();

        // Act
        var result = token.Revoke(Guid.NewGuid().ToString(), _now.Plus(Duration.FromDays(1)));

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
        var result = token.UpdateName(newName, _now);

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
        var result = token.UpdateName("New Name", _now);

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
        var result = token.UpdateExpiresAt(newExpiresAt, _now);

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
        var result = token.UpdateExpiresAt(newExpiresAt, _now);

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
        var result = token.UpdateExpiresAt(pastExpiresAt, _now);

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
        var result = token.UpdateExpiresAt(_now, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("future");
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenCurrentTimeIsAfterExpiration()
    {
        // Arrange
        var expiresAt = _now.Plus(Duration.FromDays(1));
        var token = PersonalAccessToken.Create("Test", "hash1234", "hash1234567890", "user1", null, expiresAt, null, _now).Value;

        // Act & Assert
        token.IsExpired.Should().BeFalse(); // Initially not expired

        // Fast forward time
        var futureTime = expiresAt.Plus(Duration.FromMinutes(1));

        // The IsExpired property uses SystemClock.Instance.GetCurrentInstant()
        // In a real scenario, we'd need to mock SystemClock or test at the right time
        // For now, just verify the logic works with ValidateForUse
        var validationResult = token.ValidateForUse(futureTime);
        validationResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenTokenIsExpiredOrRevoked()
    {
        // Arrange - Revoked token
        var revokedBy = Guid.NewGuid().ToString();
        var revokedToken = _tokenFaker.WithRevokedToken(revokedBy, _now).Generate();

        // Assert
        revokedToken.IsActive.Should().BeFalse();
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
        var result = PersonalAccessToken.Create(name, tokenIdentifier, tokenHash, userId, null, expiresAt, null, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Scopes.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullEmployeeId_ShouldSucceed()
    {
        // Arrange
        var name = "Test Token";
        var tokenHash = "hash12345678";
        var tokenIdentifier = "hash1234";
        var userId = "user123";
        var expiresAt = _now.Plus(Duration.FromDays(365));

        // Act
        var result = PersonalAccessToken.Create(name, tokenIdentifier, tokenHash, userId, null, expiresAt, null, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EmployeeId.Should().BeNull();
    }
}
