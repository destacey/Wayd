using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Application.StrategicThemes.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicThemes.Commands;

public sealed class SyncStrategicThemesCommandHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakeProjectPortfolioManagementDbContext _ppmContext = new();
    private readonly SyncStrategicThemesCommandHandler _handler;

    public SyncStrategicThemesCommandHandlerTests()
    {
        _handler = new SyncStrategicThemesCommandHandler(_ppmContext, Mock.Of<ILogger<SyncStrategicThemesCommandHandler>>());
    }

    public void Dispose() => _ppmContext.Dispose();

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = new StrategicThemeFaker().Generate();

        // Act
        var result = await _handler.Handle(new SyncStrategicThemesCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _ppmContext.PpmStrategicThemes.Should().ContainSingle(t => t.Id == source.Id)
            .Which.Watermarks.Should().Be(StrategicThemeWatermarks.At(Read));
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheTheme_DeletesTheCopy()
    {
        // Arrange
        var kept = new StrategicThemeFaker().Generate();
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(kept, Created));
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(new StrategicThemeFaker().Generate(), Created));

        // Act
        await _handler.Handle(new SyncStrategicThemesCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange
        var existing = new StrategicThemeFaker().Generate();
        _ppmContext.AddPpmStrategicTheme(new StrategicTheme(existing, Created));
        var createdAfterTheRead = new StrategicTheme(new StrategicThemeFaker().Generate(), AfterTheRead);
        _ppmContext.AddPpmStrategicTheme(createdAfterTheRead);

        // Act
        await _handler.Handle(new SyncStrategicThemesCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Should().Contain(t => t.Id == createdAfterTheRead.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new StrategicTheme(new StrategicThemeFaker().WithId(id).WithState(StrategicThemeState.Active).Generate(), Created);
        copy.ApplyState(StrategicThemeState.Archived, AfterTheRead);
        _ppmContext.AddPpmStrategicTheme(copy);
        var readBeforeTheArchive = new StrategicThemeFaker().WithId(id).WithName(copy.Name).WithDescription(copy.Description).WithState(StrategicThemeState.Active).Generate();

        // Act
        await _handler.Handle(new SyncStrategicThemesCommand([readBeforeTheArchive], Read), TestContext.Current.CancellationToken);

        // Assert
        _ppmContext.PpmStrategicThemes.Single(t => t.Id == id).State.Should().Be(StrategicThemeState.Archived);
    }
}
