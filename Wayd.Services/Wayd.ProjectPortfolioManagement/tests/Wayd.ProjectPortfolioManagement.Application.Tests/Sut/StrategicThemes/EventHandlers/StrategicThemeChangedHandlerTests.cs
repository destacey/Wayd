using FluentAssertions;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.StrategicManagement;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Application.StrategicThemes.EventHandlers;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Moq;
using Xunit;
using Wayd.Common.Domain.Events;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicThemes.EventHandlers;

/// <summary>
/// <see cref="StrategicThemeChangedHandler"/> keeps the PPM copy of each theme correct however the durable
/// <c>StrategicTheme*</c> events arrive: late, twice, out of order, or after the theme was deleted.
/// </summary>
public sealed class StrategicThemeChangedHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Activated = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant Archived = Created.Plus(Duration.FromMinutes(10));

    private readonly FakeProjectPortfolioManagementDbContext _ppmContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly StrategicThemeChangedHandler _handler;

    public StrategicThemeChangedHandlerTests()
    {
        _handler = new StrategicThemeChangedHandler(_ppmContext, _dispatcher.Object, Mock.Of<ILogger<StrategicThemeChangedHandler>>());
    }

    public void Dispose() => _ppmContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = new StrategicThemeFaker().WithName("Cloud Migration").WithState(StrategicThemeState.Proposed).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _ppmContext.PpmStrategicThemes.Should().ContainSingle(t => t.Id == source.Id).Subject;
        copy.Name.Should().Be("Cloud Migration");
        copy.Watermarks.Should().Be(StrategicThemeWatermarks.At(Created));
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(new StrategicThemeFaker().WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(CreatedEvent(id), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().ContainSingle(t => t.Id == id);
        _ppmContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Created_WhenDeliveredAfterTheThemeWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(CreatedEvent(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().BeEmpty();
        _ppmContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SupersededUpdated_AppliesTheDetailsButNotTheStateTheThemeHappenedToBeIn()
    {
        // Arrange — an envelope written as the superseded type before the switch; archived at Archived, an
        // edit made before the archive still carries State = Active.
        var id = Guid.CreateVersion7();
        var copy = new StrategicTheme(new StrategicThemeFaker().WithId(id).WithName("Old Name").WithState(StrategicThemeState.Active).Generate(), Created);
        copy.ApplyState(StrategicThemeState.Archived, Archived);
        _ppmContext.AddPpmStrategicTheme(copy);

#pragma warning disable CS0618 // the retired type is exactly what is under test
        var @event = new StrategicThemeUpdatedEvent(id, "New Name", "desc", StrategicThemeState.Active, EventActor.System, Activated);
#pragma warning restore CS0618

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        var theme = _ppmContext.PpmStrategicThemes.Single(t => t.Id == id);
        theme.Name.Should().Be("New Name");
        theme.State.Should().Be(StrategicThemeState.Archived);
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenOlderThanTheCopy_IsSkipped()
    {
        // Arrange — two edits processed newest first.
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(new StrategicThemeFaker().WithId(id).Generate(), Created));
        await _handler.Handle(DetailsUpdatedEvent(id, "Newest Name", Archived), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "Older Name", Activated), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Single(t => t.Id == id).Name.Should().Be("Newest Name");
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange
        var source = new StrategicThemeFaker().WithName("New Name").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source.Id, "New Name", Activated), TestContext.Current.CancellationToken);

        // Assert
        var copy = _ppmContext.PpmStrategicThemes.Should().ContainSingle(t => t.Id == source.Id).Subject;
        copy.Name.Should().Be("New Name");
        copy.Watermarks.Should().Be(StrategicThemeWatermarks.At(Activated));
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenNewerThanTheCopy_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(new StrategicThemeFaker().WithId(id).WithName("Old Name").WithState(StrategicThemeState.Active).Generate(), Created));

        var @event = DetailsUpdatedEvent(id, "New Name", Activated);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Single(t => t.Id == id).Name.Should().Be("New Name");
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ActivatedAndArchived_WhenDeliveredOutOfOrder_KeepTheLaterState()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(new StrategicThemeFaker().WithId(id).WithState(StrategicThemeState.Proposed).Generate(), Created));

        // Act
        await _handler.Handle(new StrategicThemeArchivedEvent(id, EventActor.System, Archived), TestContext.Current.CancellationToken);
        await _handler.Handle(new StrategicThemeActivatedEvent(id, EventActor.System, Activated), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Single(t => t.Id == id).State.Should().Be(StrategicThemeState.Archived);
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Activated_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(new StrategicThemeFaker().WithId(id).WithState(StrategicThemeState.Proposed).Generate(), Created));
        await _handler.Handle(new StrategicThemeActivatedEvent(id, EventActor.System, Activated), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(new StrategicThemeActivatedEvent(id, EventActor.System, Activated), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Single(t => t.Id == id).State.Should().Be(StrategicThemeState.Active);
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Archived_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange
        var source = new StrategicThemeFaker().WithState(StrategicThemeState.Archived).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(new StrategicThemeArchivedEvent(source.Id, EventActor.System, Archived), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().ContainSingle(t => t.Id == source.Id).Which.State.Should().Be(StrategicThemeState.Archived);
    }

    [Fact]
    public async Task Handle_Deleted_WhenTheCopyExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(new StrategicThemeFaker().WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(new StrategicThemeDeletedEvent(id, EventActor.System, Archived), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().BeEmpty();
        _ppmContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenRedelivered_IsNoOp()
    {
        // Arrange

        // Act
        await _handler.Handle(new StrategicThemeDeletedEvent(Guid.CreateVersion7(), EventActor.System, Archived), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.SaveChangesCallCount.Should().Be(0);
    }

    private void SourceReturns(IStrategicThemeData? theme) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetStrategicThemeDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(theme);

    private static StrategicThemeCreatedEvent CreatedEvent(Guid id) =>
        new(
            id: id,
            key: 1,
            name: "Cloud Migration",
            description: "desc",
            state: StrategicThemeState.Proposed,
            actor: EventActor.System,
            timestamp: Created);

    private static StrategicThemeDetailsUpdatedEvent DetailsUpdatedEvent(Guid id, string name, Instant timestamp) =>
        new(id, 1, name, "desc", new StrategicThemeDetails("Cloud Migration", "desc"), EventActor.System, timestamp);
}
