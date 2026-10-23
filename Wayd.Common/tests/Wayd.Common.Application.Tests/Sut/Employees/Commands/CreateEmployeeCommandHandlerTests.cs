using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public sealed class CreateEmployeeCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();

    private CreateEmployeeCommandHandler CreateHandler()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        return new CreateEmployeeCommandHandler(
            _dbContext,
            new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0))),
            currentUser.Object,
            NullLogger<CreateEmployeeCommandHandler>.Instance);
    }

    private static CreateEmployeeCommand Command(Guid? managerId) =>
        new(new PersonName("Dana", null, "Reyes"), "E-1001", null, new EmailAddress("dana.reyes@acme.example"), null, null, null, managerId);

    [Fact]
    public async Task Handle_UnknownManager_FailsAndCreatesNothing()
    {
        // Arrange
        _dbContext.AddEmployee(new EmployeeFaker().Generate());

        // Act
        var result = await CreateHandler().Handle(Command(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Manager not found.");
        _dbContext.Employees.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_KnownManager_CreatesTheEmployeeReportingToThem()
    {
        // Arrange
        var manager = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(manager);

        // Act
        var result = await CreateHandler().Handle(Command(manager.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _dbContext.Employees.Single(e => e.Id == result.Value.Id);
        created.ManagerId.Should().Be(manager.Id);
        created.EmployeeNumber.Should().Be("E-1001");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NoManager_CreatesTheEmployeeWithNone()
    {
        // Arrange
        var command = Command(null);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Employees.Single(e => e.Id == result.Value.Id).ManagerId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }
}
