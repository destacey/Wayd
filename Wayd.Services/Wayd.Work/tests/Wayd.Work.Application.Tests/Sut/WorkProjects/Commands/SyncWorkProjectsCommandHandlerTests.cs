using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProjects.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProjects.Commands;

public sealed class SyncWorkProjectsCommandHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakeWorkDbContext _workDbContext = new();
    private readonly SyncWorkProjectsCommandHandler _handler;

    public SyncWorkProjectsCommandHandlerTests()
    {
        _handler = new SyncWorkProjectsCommandHandler(_workDbContext, Mock.Of<ILogger<SyncWorkProjectsCommandHandler>>());
    }

    public void Dispose() => _workDbContext.Dispose();

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = new WorkProjectFaker().Generate();

        // Act
        var result = await _handler.Handle(new SyncWorkProjectsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _workDbContext.WorkProjects.Should().ContainSingle(p => p.Id == source.Id)
            .Which.Watermarks.Should().Be(WorkProjectWatermarks.At(Read));
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheProject_DeletesTheCopy()
    {
        // Arrange
        var kept = new WorkProjectFaker().Generate();
        _workDbContext.AddWorkProject(new WorkProject(kept, Created));
        _workDbContext.AddWorkProject(new WorkProject(new WorkProjectFaker().Generate(), Created));

        // Act
        await _handler.Handle(new SyncWorkProjectsCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange
        var existing = new WorkProjectFaker().Generate();
        _workDbContext.AddWorkProject(new WorkProject(existing, Created));
        var createdAfterTheRead = new WorkProject(new WorkProjectFaker().Generate(), AfterTheRead);
        _workDbContext.AddWorkProject(createdAfterTheRead);

        // Act
        await _handler.Handle(new SyncWorkProjectsCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().Contain(p => p.Id == createdAfterTheRead.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new WorkProject(new WorkProjectFaker().WithId(id).WithKey(new ProjectKey("OLDKEY")).Generate(), Created);
        copy.ApplyKey(new ProjectKey("NEWKEY"), AfterTheRead);
        _workDbContext.AddWorkProject(copy);
        var readBeforeTheRekey = new WorkProjectFaker().WithId(id).WithKey(new ProjectKey("OLDKEY")).WithName(copy.Name).WithDescription(copy.Description).Generate();

        // Act
        await _handler.Handle(new SyncWorkProjectsCommand([readBeforeTheRekey], Read), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Single(p => p.Id == id).Key.Value.Should().Be("NEWKEY");
    }
}
