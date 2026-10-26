using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class CreateIntakeDeskCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private CreateIntakeDeskCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    private static CreateIntakeDeskCommand Command(string name, params IntakeDeskMemberDetails[] members) =>
        new(name, "Laptops, access and internal tools", IntakeDeskVisibility.SummaryOnly, true, true, true, members);

    [Fact]
    public async Task Handle_WhenValid_CreatesTheDeskWithItsMembersInTheirRoles()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var agent = _scenario.Employee();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(
            Command(" IT ", new IntakeDeskMemberDetails(deskAdmin.Id, IntakeDeskRole.Admin), new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var desk = _scenario.DbContext.IntakeDesks.Single();
        desk.Name.Should().Be("IT");
        desk.Visibility.Should().Be(IntakeDeskVisibility.SummaryOnly);
        desk.RoleOf(deskAdmin.Id).Should().Be(IntakeDeskRole.Admin);
        desk.RoleOf(agent.Id).Should().Be(IntakeDeskRole.Agent);
        desk.Members.Should().HaveCount(2);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithANameAlreadyTaken_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var existing = _scenario.Desk();

        // Act
        var result = await CreateHandler().Handle(Command(existing.Name), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A desk with this name already exists.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithAnInactiveMember_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(
            Command("IT", new IntakeDeskMemberDetails(_scenario.Employee(isActive: false).Id, IntakeDeskRole.Agent)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Every member must be an active employee.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
