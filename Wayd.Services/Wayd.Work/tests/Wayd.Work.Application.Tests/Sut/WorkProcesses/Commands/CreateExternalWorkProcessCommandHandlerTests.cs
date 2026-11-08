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

public sealed class CreateExternalWorkProcessCommandHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly CreateExternalWorkProcessCommandHandler _handler;

    public CreateExternalWorkProcessCommandHandlerTests()
    {
        var dateTimeProvider = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 10, 31, 12, 0)));
        _handler = new CreateExternalWorkProcessCommandHandler(_dbContext, dateTimeProvider, NullLogger<CreateExternalWorkProcessCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_CreatesTheProcess_Active()
    {
        // Arrange
        var process = ExternalProcess("Agile", "The Agile process.");

        // Act
        var result = await _handler.Handle(new CreateExternalWorkProcessCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        var workProcess = _dbContext.WorkProcesses.Single();
        workProcess.ExternalId.Should().Be(process.Id);
        workProcess.Name.Should().Be("Agile");
        workProcess.Description.Should().Be("The Agile process.");
        // A workspace can only be created on an active work process.
        workProcess.IsActive.Should().BeTrue();
        result.Value.InternalId.Should().Be(workProcess.Id);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheExternalProcessAlreadyExists_Fails()
    {
        // Arrange
        var process = ExternalProcess("Agile");
        _dbContext.AddWorkProcess(new WorkProcessFaker().AsExternal(process.Id).Generate());

        // Act
        var result = await _handler.Handle(new CreateExternalWorkProcessCommand(process), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
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
