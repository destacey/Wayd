using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime.Testing;
using NodaTime.Extensions;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.StrategicManagement.Application.StrategicThemes.Commands;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;
using Wayd.StrategicManagement.Application.Tests.Infrastructure;
using Wayd.StrategicManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.StrategicManagement.Application.Tests.Sut.StrategicThemes.Commands;

public class ImportStrategicThemesCommandHandlerTests : IDisposable
{
    private readonly FakeStrategicManagementDbContext _dbContext;
    private readonly ImportStrategicThemesCommandHandler _handler;
    private readonly Mock<ILogger<ImportStrategicThemesCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    public ImportStrategicThemesCommandHandlerTests()
    {
        _dbContext = new FakeStrategicManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportStrategicThemesCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));

        _handler = new ImportStrategicThemesCommandHandler(_dbContext, _dateTimeProvider, _mockLogger.Object);
    }

    [Theory]
    [InlineData(StrategicThemeState.Proposed)]
    [InlineData(StrategicThemeState.Active)]
    [InlineData(StrategicThemeState.Archived)]
    public async Task Handle_CreatesThemeInTheRequestedState(StrategicThemeState state)
    {
        // Arrange
        // Create accepts the state directly, so no activate/archive transition is needed.
        var command = new ImportStrategicThemesCommand([new ImportStrategicThemeDto("Reliability", "Keep the lights on", state)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var theme = _dbContext.StrategicThemes.Single();
        theme.Name.Should().Be("Reliability");
        theme.State.Should().Be(state);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SavesOnce_ForTheWholeBatch()
    {
        // Arrange
        var command = new ImportStrategicThemesCommand([
            new ImportStrategicThemeDto("Reliability", "Keep the lights on", StrategicThemeState.Active),
            new ImportStrategicThemeDto("Efficiency", "Do more with less", StrategicThemeState.Active),
            new ImportStrategicThemeDto("Growth", "Reach new markets", StrategicThemeState.Proposed),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.StrategicThemes.Should().HaveCount(3);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Fails_WhenANameIsDuplicatedWithinTheBatch()
    {
        // Arrange
        // Themes are resolved by name from the program and project imports, so a duplicate is ambiguous.
        var command = new ImportStrategicThemesCommand([
            new ImportStrategicThemeDto("Reliability", "Keep the lights on", StrategicThemeState.Active),
            new ImportStrategicThemeDto("reliability", "Duplicate", StrategicThemeState.Active),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenAThemeAlreadyExists()
    {
        // Arrange
        _dbContext.AddStrategicTheme(new StrategicThemeFaker().WithName("Reliability").Generate());

        var command = new ImportStrategicThemesCommand([new ImportStrategicThemeDto("Reliability", "Keep the lights on", StrategicThemeState.Active)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exist");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
