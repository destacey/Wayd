using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProcesses.Commands;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Commands;

public sealed class UpdateExternalWorkProcessCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly UpdateExternalWorkProcessCommandHandler _handler;

    public UpdateExternalWorkProcessCommandHandlerTests()
    {
        var dateTimeProvider = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 11, 8, 12, 0)));
        _handler = new UpdateExternalWorkProcessCommandHandler(_dbContext, dateTimeProvider, NullLogger<UpdateExternalWorkProcessCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ShouldTakeTheSourcesNameAndDescription()
    {
        // Arrange
        var process = ExternalProcess("Agile 2", "Renamed at the source.");
        var workProcess = new WorkProcessFaker().AsExternal(process.Id).WithName("Agile").WithDescription("Before.").Generate();
        _dbContext.AddWorkProcess(workProcess);

        // Act
        var result = await _handler.Handle(new UpdateExternalWorkProcessCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (workProcess.Name, workProcess.Description).Should().Be(("Agile 2", "Renamed at the source."));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotSave_WhenTheProcessIsUnchangedOnceTrimmed()
    {
        // Arrange — the source pads the name; the process stores it trimmed
        var process = ExternalProcess(" Agile ", "Same.");
        var workProcess = new WorkProcessFaker().AsExternal(process.Id).WithName("Agile").WithDescription("Same.").Generate();
        _dbContext.AddWorkProcess(workProcess);

        // Act
        var result = await _handler.Handle(new UpdateExternalWorkProcessCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProcessIsNotInWayd()
    {
        // Arrange
        _dbContext.AddWorkProcess(new WorkProcessFaker().AsExternal(Guid.NewGuid()).Generate());

        // Act
        var result = await _handler.Handle(new UpdateExternalWorkProcessCommand(ExternalProcess("Agile")), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static IExternalWorkProcess ExternalProcess(string name, string? description = null)
    {
        var process = new Mock<IExternalWorkProcess>();
        process.Setup(p => p.Id).Returns(Guid.CreateVersion7());
        process.Setup(p => p.Name).Returns(name);
        process.Setup(p => p.Description).Returns(description);
        return process.Object;
    }
}
