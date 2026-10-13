using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Organization.Application.TeamMemberRoles.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;

namespace Wayd.Organization.Application.Tests.Sut.TeamMemberRoles.Commands;

public class CreateTeamMemberRoleCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly CreateTeamMemberRoleCommandHandler _handler;

    public CreateTeamMemberRoleCommandHandlerTests()
    {
        _dbContext = new FakeOrganizationDbContext();

        _handler = new CreateTeamMemberRoleCommandHandler(
            _dbContext,
            new Mock<ILogger<CreateTeamMemberRoleCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateRole_WithValidName()
    {
        // Arrange
        var command = new CreateTeamMemberRoleCommand("Tech Lead", "Tech Lead role");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldSetWhetherTheRoleCountsTowardDeliveryCapacity(bool countsTowardDeliveryCapacity)
    {
        // Arrange
        var command = new CreateTeamMemberRoleCommand("Manager", null, countsTowardDeliveryCapacity);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.TeamMemberRoles.Single().CountsTowardDeliveryCapacity.Should().Be(countsTowardDeliveryCapacity);
    }

    [Fact]
    public async Task Handle_ShouldReturnNewId_OnSuccess()
    {
        // Arrange
        var command = new CreateTeamMemberRoleCommand("Engineer", "Engineer role");

        // Act
        var result1 = await _handler.Handle(command, TestContext.Current.CancellationToken);
        var result2 = await _handler.Handle(new CreateTeamMemberRoleCommand("Manager", "Manager role"), TestContext.Current.CancellationToken);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Should().NotBe(result2.Value);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
