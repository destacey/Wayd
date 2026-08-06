using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Commands;

public class CreateRoadmapCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly Mock<ILogger<CreateRoadmapCommandHandler>> _mockLogger = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();

    private static readonly LocalDateRange AnyDateRange =
        new(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31));

    public CreateRoadmapCommandHandlerTests() =>
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currentEmployeeId);

    private CreateRoadmapCommandHandler CreateSut() =>
        new(_dbContext, _mockCurrentPrincipal.Object, _mockLogger.Object);

    private static CreateRoadmapCommand CommandFor(params Guid[] managerIds) =>
        new("Roadmap", null, AnyDateRange, [.. managerIds], Visibility.Public);

    [Fact]
    public async Task Handle_WhenCallerIsAManager_CreatesAndSaves()
    {
        // Arrange
        var command = CommandFor(_currentEmployeeId);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenCallerHasNoEmployeeLink_RefusesWithoutSaving()
    {
        // Arrange — enforced in the handler, not only the validator: Wolverine requires handlers to
        // be public, so this can be called directly, skipping both the validator and the middleware.
        _mockCurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var command = CommandFor(Guid.NewGuid());
        var sut = CreateSut();

        // Act
        var act = () => sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*isn't linked to an employee record*");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAmongTheManagers_FailsWithoutSaving()
    {
        // Arrange — the domain accepts whatever manager ids it is given, so a direct call could
        // otherwise create a roadmap the caller has no ability to administer.
        var command = CommandFor(Guid.NewGuid());
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be a manager");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
