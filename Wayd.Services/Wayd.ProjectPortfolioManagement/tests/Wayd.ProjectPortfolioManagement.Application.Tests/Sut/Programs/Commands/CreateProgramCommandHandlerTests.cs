using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Commands;

public sealed class CreateProgramCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
    private readonly CreateProgramCommandHandler _handler;

    public CreateProgramCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());

        _handler = new CreateProgramCommandHandler(
            _dbContext, currentUser.Object, Mock.Of<ILogger<CreateProgramCommandHandler>>(), _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ShouldAttachAStrategicTheme_WhenItIsActive()
    {
        // Arrange
        var (portfolio, command) = CommandWithTheme(StrategicThemeState.Active, out var theme);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        portfolio.Programs.Should().ContainSingle()
            .Which.StrategicThemeTags.Should().ContainSingle(t => t.StrategicThemeId == theme.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(StrategicThemeState.Proposed)]
    [InlineData(StrategicThemeState.Archived)]
    public async Task Handle_ShouldFail_WhenAStrategicThemeIsNotActive(StrategicThemeState state)
    {
        // Arrange
        var (portfolio, command) = CommandWithTheme(state, out _);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("One or more Strategic Themes are not active.");
        portfolio.Programs.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAStrategicThemeDoesNotExist()
    {
        // Arrange
        var (portfolio, command) = CommandWithTheme(StrategicThemeState.Active, out _);
        command = command with { StrategicThemeIds = [Guid.NewGuid()] };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("One or more Strategic Themes not found.");
        portfolio.Programs.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    /// <summary>A valid command for a new program in an active portfolio, tagged with one theme in the given state.</summary>
    private (ProjectPortfolio Portfolio, CreateProgramCommand Command) CommandWithTheme(
        StrategicThemeState state, out StrategicTheme theme)
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        theme = new StrategicThemeFaker().WithState(state).Generate();
        _dbContext.AddPortfolio(portfolio);
        _dbContext.AddPpmStrategicTheme(theme);

        var command = new CreateProgramCommand("Platform", "Description", null, portfolio.Id, null, null, null, [theme.Id]);

        return (portfolio, command);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
