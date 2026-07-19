using FluentAssertions;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Application.StrategicThemes.EventHandlers;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Moq;
using Xunit;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicThemes.EventHandlers;

/// <summary>
/// <see cref="StrategicThemeChangedHandler"/> replicates StrategicManagement theme changes into the PPM
/// <c>PpmStrategicThemes</c> projection. Because it is delivered durably, its contract is: idempotent guards
/// short-circuit a redelivery, and any real failure PROPAGATES (Wolverine's retry/dead-letter governs it)
/// rather than being swallowed.
/// </summary>
public sealed class StrategicThemeChangedHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly FakeProjectPortfolioManagementDbContext _ppmContext = new();
    private readonly StrategicThemeChangedHandler _handler;

    public StrategicThemeChangedHandlerTests()
    {
        _handler = new StrategicThemeChangedHandler(_ppmContext, Mock.Of<ILogger<StrategicThemeChangedHandler>>());
    }

    public void Dispose() => _ppmContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenThemeIsNew_AddsProjectionAndSaves()
    {
        // Arrange
        var @event = CreatedEvent(Guid.CreateVersion7(), 1, "Cloud Migration");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().ContainSingle(t => t.Id == @event.Id);
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenThemeAlreadyExists_IsIdempotentNoOp()
    {
        // Arrange — a redelivery, or a race with the Hangfire bulk sync, finds the projection already there.
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicThemeFaker().WithId(id).Generate());

        var @event = CreatedEvent(id, 2, "Duplicate");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert — no second row, no save.
        _ppmContext.PpmStrategicThemes.Should().ContainSingle(t => t.Id == id);
        _ppmContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Updated_WhenThemeExists_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicThemeFaker().WithId(id).WithName("Old Name").Generate());

        var @event = new StrategicThemeUpdatedEvent(id, "New Name", "desc", StrategicThemeState.Active, Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Single(t => t.Id == id).Name.Should().Be("New Name");
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenThemeMissing_IsNoOp()
    {
        // Arrange — out-of-order delivery: the update arrives before the create was applied.
        var @event = new StrategicThemeUpdatedEvent(Guid.CreateVersion7(), "Whatever", "desc", StrategicThemeState.Active, Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().BeEmpty();
        _ppmContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Deleted_WhenThemeExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicThemeFaker().WithId(id).Generate());

        var @event = new StrategicThemeDeletedEvent(id, Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().BeEmpty();
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenThemeMissing_IsIdempotentNoOp()
    {
        // Arrange — a redelivery of a delete that already ran; the goal state (absent) already holds.
        var @event = new StrategicThemeDeletedEvent(Guid.CreateVersion7(), Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.SaveChangesCallCount.Should().Be(0);
    }

    private static StrategicThemeCreatedEvent CreatedEvent(Guid id, int key, string name) =>
        new(
            id: id,
            key: key,
            name: name,
            description: "desc",
            state: StrategicThemeState.Active,
            timestamp: Now);
}
