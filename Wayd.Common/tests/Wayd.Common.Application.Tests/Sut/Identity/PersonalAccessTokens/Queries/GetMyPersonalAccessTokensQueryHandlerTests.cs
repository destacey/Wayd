using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Identity.PersonalAccessTokens.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Application.Tests.Sut.Identity.PersonalAccessTokens.Queries;

public class GetMyPersonalAccessTokensQueryHandlerTests
{
    private const string UserId = "user-1";

    private static readonly Instant Now =
        Instant.FromUtc(2026, 7, 2, 12, 0, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(Now.ToDateTimeUtc());
    private readonly Mock<ICurrentUser> _currentUser = new();

    public GetMyPersonalAccessTokensQueryHandlerTests()
    {
        _currentUser.Setup(x => x.GetUserId()).Returns(UserId);
    }

    private GetMyPersonalAccessTokensQueryHandler CreateHandler() =>
        new(_dbContext, _currentUser.Object, _dateTimeProvider);

    private void SeedActiveToken() =>
        _dbContext.PersonalAccessTokens.Add(
            new PersonalAccessTokenFaker(Now)
                .WithUserId(UserId)
                .WithExpiresAt(Now.Plus(Duration.FromDays(30)))
                .Generate());

    private void SeedExpiredToken() =>
        _dbContext.PersonalAccessTokens.Add(
            new PersonalAccessTokenFaker(Now)
                .WithUserId(UserId)
                .WithExpiresAt(Now.Minus(Duration.FromDays(1)))
                .Generate());

    private void SeedRevokedToken() =>
        _dbContext.PersonalAccessTokens.Add(
            new PersonalAccessTokenFaker(Now)
                .WithUserId(UserId)
                .WithExpiresAt(Now.Plus(Duration.FromDays(30)))
                .AsRevoked(revokedAt: Now.Minus(Duration.FromDays(1)))
                .Generate());

    [Fact]
    public async Task Handle_ShouldMarkNonExpiredNonRevokedToken_AsActive()
    {
        // Arrange
        SeedActiveToken();

        // Act
        var result = await CreateHandler().Handle(
            new GetMyPersonalAccessTokensQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var token = result.Value.Should().ContainSingle().Subject;
        token.IsActive.Should().BeTrue();
        token.IsExpired.Should().BeFalse();
        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldMarkTokenPastExpiry_AsExpiredAndNotActive()
    {
        // Arrange
        SeedExpiredToken();

        // Act
        var result = await CreateHandler().Handle(
            new GetMyPersonalAccessTokensQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var token = result.Value.Should().ContainSingle().Subject;
        token.IsExpired.Should().BeTrue();
        token.IsActive.Should().BeFalse();
        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldMarkRevokedToken_AsRevokedAndNotActive()
    {
        // Arrange
        SeedRevokedToken();

        // Act
        var result = await CreateHandler().Handle(
            new GetMyPersonalAccessTokensQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var token = result.Value.Should().ContainSingle().Subject;
        token.IsRevoked.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnTokensForTheCurrentUser()
    {
        // Arrange
        SeedActiveToken();
        _dbContext.PersonalAccessTokens.Add(
            new PersonalAccessTokenFaker(Now)
                .WithUserId("someone-else")
                .Generate());

        // Act
        var result = await CreateHandler().Handle(
            new GetMyPersonalAccessTokensQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().ContainSingle();
    }
}
