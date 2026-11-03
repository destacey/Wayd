using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class CreateStoryMapCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly CreateStoryMapCommandHandler _handler;
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal;

    private readonly Guid _employeeId;

    public CreateStoryMapCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        var currentEmployee = new EmployeeFaker().Generate();
        _employeeId = currentEmployee.Id;
        _dbContext.AddEmployee(currentEmployee);
        _mockCurrentPrincipal = new Mock<ICurrentPrincipal>();
        _mockCurrentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);

        _handler = new CreateStoryMapCommandHandler(_dbContext, _mockCurrentPrincipal.Object, new Mock<ILogger<CreateStoryMapCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateAPublicMapOwnedByTheCaller()
    {
        // Arrange
        var command = new CreateStoryMapCommand("Checkout redesign", "A description");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Id.Should().NotBe(Guid.Empty);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        var created = _dbContext.StoryMaps.Single();
        created.Owners.Select(o => o.EmployeeId).Should().Equal(_employeeId);
        created.Visibility.Should().Be(Visibility.Public);
        created.EditAccess.Should().Be(StoryMapEditAccess.AnyoneWhoCanView);
    }

    [Fact]
    public async Task Handle_WithAccess_ShouldSetOwnersEditorsAndVisibility()
    {
        // Arrange
        var coOwner = new EmployeeFaker().Generate();
        var editor = new EmployeeFaker().Generate();
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        _dbContext.AddEmployee(coOwner);
        _dbContext.AddEmployee(editor);
        _dbContext.AddPlanningTeam(team);
        var command = new CreateStoryMapCommand("Checkout redesign", null, Visibility.Private, StoryMapEditAccess.OwnersAndEditors, [coOwner.Id], [editor.Id], [team.Id]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var created = _dbContext.StoryMaps.Single();
        created.Owners.Select(o => o.EmployeeId).Should().BeEquivalentTo([_employeeId, coOwner.Id]);
        created.Editors.Select(e => e.EmployeeId).Should().Equal(editor.Id);
        created.EditorTeams.Select(t => t.TeamId).Should().Equal(team.Id);
        created.Visibility.Should().Be(Visibility.Private);
        created.EditAccess.Should().Be(StoryMapEditAccess.OwnersAndEditors);
    }

    [Fact]
    public async Task Handle_UnknownEditor_ShouldFailWithoutSaving()
    {
        // Arrange
        var command = new CreateStoryMapCommand("Checkout redesign", null, EditorIds: [Guid.NewGuid()]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CallerWithNoEmployee_ShouldThrowForbidden()
    {
        // Arrange — the creator becomes the first owner, so a map cannot be created without one.
        _mockCurrentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => _handler.Handle(new CreateStoryMapCommand("Checkout redesign", null), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
