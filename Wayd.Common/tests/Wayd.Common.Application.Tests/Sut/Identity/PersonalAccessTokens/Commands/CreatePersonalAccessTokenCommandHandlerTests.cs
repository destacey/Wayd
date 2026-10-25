using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.Identity.PersonalAccessTokens.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Authorization;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Identity.PersonalAccessTokens.Commands;

public sealed class CreatePersonalAccessTokenCommandHandlerTests
{
    private const string UserId = "user-1";

    private static readonly Instant Now = Instant.FromUtc(2026, 7, 2, 12, 0, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(Now.ToDateTimeUtc());
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ITokenHashingService> _hashing = new();

    public CreatePersonalAccessTokenCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns(UserId);
        _hashing.Setup(h => h.GenerateToken()).Returns("abcd1234-plain");
        _hashing.Setup(h => h.HashToken("abcd1234-plain")).Returns(("abcd1234", "hashed"));
    }

    private CreatePersonalAccessTokenCommandHandler CreateHandler() =>
        new(_dbContext, _currentUser.Object, _dateTimeProvider, _hashing.Object, NullLogger<CreatePersonalAccessTokenCommandHandler>.Instance);

    private CreatePersonalAccessTokenCommandValidator CreateValidator() =>
        new(_dbContext, _currentUser.Object, _dateTimeProvider);

    [Theory]
    [InlineData(TokenScope.Read, "wayd:read")]
    [InlineData(TokenScope.Write, "wayd:write")]
    public async Task Handle_StoresTheScope_AndAnswersIt(TokenScope scope, string stored)
    {
        // Arrange
        var command = new CreatePersonalAccessTokenCommand("CI", Now.Plus(Duration.FromDays(30)), scope);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Scope.Should().Be(scope);
        result.Value.Token.Should().Be("abcd1234-plain");
        _dbContext.PersonalAccessTokens.Should().ContainSingle().Which.Scopes.Should().Be(stored);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public void Command_NamingNoScope_IsWrite()
    {
        // Act
        var command = new CreatePersonalAccessTokenCommand("CI", Now.Plus(Duration.FromDays(30)));

        // Assert
        command.Scope.Should().Be(TokenScope.Write);
    }

    [Fact]
    public async Task Validator_RefusesAScopeThatIsNotDefined()
    {
        // Arrange — the API's enum converter accepts a number, so any value can arrive
        var command = new CreatePersonalAccessTokenCommand("CI", Now.Plus(Duration.FromDays(30)), (TokenScope)0);

        // Act
        var result = await CreateValidator().ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(CreatePersonalAccessTokenCommand.Scope));
    }

    [Theory]
    [InlineData(TokenScope.Read)]
    [InlineData(TokenScope.Write)]
    public async Task Validator_AcceptsEachScope(TokenScope scope)
    {
        // Arrange
        var command = new CreatePersonalAccessTokenCommand("CI", Now.Plus(Duration.FromDays(30)), scope);

        // Act
        var result = await CreateValidator().ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
