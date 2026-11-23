using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives.Commands;

public sealed class UpdateStrategicInitiativeCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly LocalDateRange _newDates = new(new LocalDate(2027, 1, 1), new LocalDate(2027, 6, 30));
    private readonly Guid _existingOwnerId = Guid.NewGuid();

    public UpdateStrategicInitiativeCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    private UpdateStrategicInitiativeCommandHandler CreateHandler() => new(
        _dbContext, Mock.Of<ILogger<UpdateStrategicInitiativeCommandHandler>>(), _currentUser.Object, _dateTimeProvider);

    private StrategicInitiative SeedInitiative()
    {
        var initiative = new StrategicInitiativeFaker(_dateTimeProvider).WithName("Original")
            .WithRoles(new() { [StrategicInitiativeRole.Owner] = [_existingOwnerId] })
            .AsActive(_dateTimeProvider);
        _dbContext.AddStrategicInitiative(initiative);
        return initiative;
    }

    [Fact]
    public async Task Handle_ShouldUpdateTheDetailsTimelineAndRoles()
    {
        // Arrange
        var initiative = SeedInitiative();
        var owner = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(owner);
        var command = new UpdateStrategicInitiativeCommand(initiative.Id, "Renamed", "New description", _newDates, null, [owner.Id]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        initiative.Name.Should().Be("Renamed");
        initiative.DateRange.Should().Be(_newDates);
        initiative.Roles.Should().ContainSingle(r => r.Role == StrategicInitiativeRole.Owner && r.EmployeeId == owner.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenARoleNamesAnUnknownEmployee()
    {
        // Arrange
        var initiative = SeedInitiative();
        var command = new UpdateStrategicInitiativeCommand(initiative.Id, "Renamed", "New description", _newDates, null, [Guid.NewGuid()]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("One or more employees were not found.");
        initiative.Roles.Should().ContainSingle(r => r.Role == StrategicInitiativeRole.Owner && r.EmployeeId == _existingOwnerId);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheInitiativeDoesNotExist()
    {
        // Arrange
        var initiative = SeedInitiative();
        var command = new UpdateStrategicInitiativeCommand(Guid.NewGuid(), "Renamed", "New description", _newDates, null, null);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Strategic Initiative not found.");
        initiative.Name.Should().Be("Original");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
