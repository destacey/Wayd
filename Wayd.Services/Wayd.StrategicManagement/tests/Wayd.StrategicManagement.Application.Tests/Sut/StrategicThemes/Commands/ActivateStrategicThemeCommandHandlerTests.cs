using FluentAssertions;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.StrategicManagement.Application.StrategicThemes.Commands;
using Wayd.StrategicManagement.Application.Tests.Infrastructure;
using Wayd.StrategicManagement.Domain.Tests.Data;

namespace Wayd.StrategicManagement.Application.Tests.Sut.StrategicThemes.Commands;

public sealed class ActivateStrategicThemeCommandHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeStrategicManagementDbContext _dbContext = new();
    private readonly StrategicThemeFaker _faker = new();
    private readonly ActivateStrategicThemeCommandHandler _sut;

    public ActivateStrategicThemeCommandHandlerTests()
    {
        _mocker.Use<IStrategicManagementDbContext>(_dbContext);
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);

        _sut = _mocker.CreateInstance<ActivateStrategicThemeCommandHandler>();
    }

    [Fact]
    public async Task Handle_ActivatesAndSaves_WhenThemeIsProposed()
    {
        // Arrange
        var theme = _faker.AsProposed().Generate();
        _dbContext.AddStrategicTheme(theme);

        // Act
        var result = await _sut.Handle(new ActivateStrategicThemeCommand(theme.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        theme.State.Should().Be(StrategicThemeState.Active);
        theme.DomainEvents.OfType<StrategicThemeActivatedEvent>().Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsTheRuleAndSavesNothing_WhenThemeIsAlreadyActive()
    {
        // Arrange
        var theme = _faker.AsActive().Generate();
        _dbContext.AddStrategicTheme(theme);

        // Act
        var result = await _sut.Handle(new ActivateStrategicThemeCommand(theme.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed strategic themes can be activated.");
        theme.State.Should().Be(StrategicThemeState.Active);
        theme.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundAndSavesNothing_WhenThemeDoesNotExist()
    {
        // Act
        var result = await _sut.Handle(new ActivateStrategicThemeCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Strategic Theme not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
