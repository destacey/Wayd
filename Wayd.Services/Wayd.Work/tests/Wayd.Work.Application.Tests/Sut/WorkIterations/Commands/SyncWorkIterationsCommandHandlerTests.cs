using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkIterations.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkIterations.Commands;

public sealed class SyncWorkIterationsCommandHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakeWorkDbContext _workDbContext = new();
    private readonly SyncWorkIterationsCommandHandler _handler;

    public SyncWorkIterationsCommandHandlerTests()
    {
        _handler = new SyncWorkIterationsCommandHandler(_workDbContext, Mock.Of<ILogger<SyncWorkIterationsCommandHandler>>());
    }

    public void Dispose() => _workDbContext.Dispose();

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = new WorkIterationFaker().Generate();

        // Act
        var result = await _handler.Handle(new SyncWorkIterationsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _workDbContext.WorkIterations.Should().ContainSingle(i => i.Id == source.Id)
            .Which.Watermarks.Record.Should().Be(Read);
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheIteration_DeletesTheCopy()
    {
        // Arrange
        var kept = new WorkIterationFaker().Generate();
        _workDbContext.AddWorkIteration(new WorkIteration(kept, Created));
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().Generate(), Created));

        // Act
        await _handler.Handle(new SyncWorkIterationsCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange
        var existing = new WorkIterationFaker().Generate();
        _workDbContext.AddWorkIteration(new WorkIteration(existing, Created));
        var createdAfterTheRead = new WorkIteration(new WorkIterationFaker().Generate(), AfterTheRead);
        _workDbContext.AddWorkIteration(createdAfterTheRead);

        // Act
        await _handler.Handle(new SyncWorkIterationsCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().Contain(i => i.Id == createdAfterTheRead.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new WorkIteration(new WorkIterationFaker().WithId(id).WithName("Sprint 1").Generate(), Created);
        var renamed = new WorkIterationFaker().WithId(id).WithKey(copy.Key).WithName("Sprint 2").WithType(copy.Type)
            .WithState(copy.State).WithDateRange(copy.DateRange).WithTeamId(copy.TeamId).Generate();
        copy.ApplyRecord(renamed, EventActor.System, AfterTheRead);
        _workDbContext.AddWorkIteration(copy);
        var readBeforeTheRename = new WorkIterationFaker().WithId(id).WithKey(copy.Key).WithName("Sprint 1").WithType(copy.Type)
            .WithState(copy.State).WithDateRange(copy.DateRange).WithTeamId(copy.TeamId).Generate();

        // Act
        await _handler.Handle(new SyncWorkIterationsCommand([readBeforeTheRename], Read), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Single(i => i.Id == id).Name.Should().Be("Sprint 2");
    }
}
