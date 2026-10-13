using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Organization.Application.TeamMemberRoles.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;

namespace Wayd.Organization.Application.Tests.Sut.TeamMemberRoles.Commands;

public class UpdateTeamMemberRoleCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly UpdateTeamMemberRoleCommandHandler _handler;

    public UpdateTeamMemberRoleCommandHandlerTests()
    {
        _dbContext = new FakeOrganizationDbContext();

        _handler = new UpdateTeamMemberRoleCommandHandler(
            _dbContext,
            new Mock<ILogger<UpdateTeamMemberRoleCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldUpdateName_WhenRoleExists()
    {
        // Arrange
        var role = TeamMemberRole.Create("Tech Lead", "Tech Lead role").Value;
        _dbContext.AddTeamMemberRole(role);

        var command = new UpdateTeamMemberRoleCommand(role.Id, "Senior Tech Lead", "Senior Tech Lead role");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        role.Name.Should().Be("Senior Tech Lead");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveDeliveryCapacityAlone_WhenItIsNotGiven()
    {
        // Arrange
        var role = TeamMemberRole.Create("Manager", null).Value;
        role.SetCountsTowardDeliveryCapacity(false);
        _dbContext.AddTeamMemberRole(role);

        // Act
        var result = await _handler.Handle(new UpdateTeamMemberRoleCommand(role.Id, "People Manager", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        role.CountsTowardDeliveryCapacity.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldChangeDeliveryCapacity_WhenItIsGiven()
    {
        // Arrange
        var role = TeamMemberRole.Create("Manager", null).Value;
        _dbContext.AddTeamMemberRole(role);

        // Act
        var result = await _handler.Handle(new UpdateTeamMemberRoleCommand(role.Id, "Manager", null, false), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        role.CountsTowardDeliveryCapacity.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoleNotFound()
    {
        // Arrange
        var command = new UpdateTeamMemberRoleCommand(Guid.NewGuid(), "Engineer", "Engineer role");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
