using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class UpdateIntakeDeskCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private UpdateIntakeDeskCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    private static UpdateIntakeDeskCommand Unchanged(IntakeDesk desk) =>
        new(desk.Id, desk.Name, desk.Description, desk.Visibility, desk.IsRequesterSelectable, desk.IsOpen,
            desk.RequesterMayAddParticipants, desk.OffersAllChannels, [.. desk.Channels.Select(c => c.ChannelId)],
            [.. desk.Members.Select(m => new IntakeDeskMemberDetails(m.EmployeeId, m.Role))], desk.Revision);

    private static IntakeDeskMemberDetails Agent(Guid employeeId) => new(employeeId, IntakeDeskRole.Agent);

    private static IntakeDeskMemberDetails Admin(Guid employeeId) => new(employeeId, IntakeDeskRole.Admin);

    [Fact]
    public async Task Handle_ByADeskAdmin_ChangesTheDetailsSettingsAndAgents()
    {
        // Arrange — a Desk Admin holding no claim runs their own desk
        var deskAdmin = _scenario.Employee();
        var newAgent = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        _scenario.SignedInAs(deskAdmin);
        var command = Unchanged(desk) with
        {
            Name = "IT services",
            IsOpen = false,
            IsRequesterSelectable = false,
            Members = [Admin(deskAdmin.Id), Agent(newAgent.Id)],
        };

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.Name.Should().Be("IT services");
        desk.IsOpen.Should().BeFalse();
        desk.IsRequesterSelectable.Should().BeFalse();
        desk.RoleOf(newAgent.Id).Should().Be(IntakeDeskRole.Agent);
        desk.DomainEvents.Select(e => e.GetType()).Should().BeEquivalentTo(
        [
            typeof(IntakeDeskDetailsUpdatedEvent),
            typeof(IntakeDeskClosedEvent),
            typeof(IntakeDeskRequesterSettingsChangedEvent),
            typeof(IntakeDeskMembersChangedEvent),
        ]);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PreparedBeforeAnotherChange_IsRefusedAndUndoesNothing()
    {
        // Arrange — another Desk Admin named an agent after this page was loaded
        var deskAdmin = _scenario.Employee();
        var agentAddedSince = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var stale = Unchanged(desk) with { Name = "Renamed" };
        desk.ChangeMembers([Admin(deskAdmin.Id), Agent(agentAddedSince.Id)], EventActor.System, _scenario.Clock.Now);
        desk.ClearDomainEvents();
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(stale, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.StaleError);
        desk.RoleOf(agentAddedSince.Id).Should().Be(IntakeDeskRole.Agent);
        desk.Name.Should().NotBe("Renamed");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheSaveIsRefusedByTheDatabase_ReportsTheDeskAsChanged()
    {
        // Arrange — a request was assigned to the removed agent after the requests to unassign were read
        var deskAdmin = _scenario.Employee();
        var leaving = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id), Agent(leaving.Id));
        _scenario.SignedInAs(deskAdmin);
        _scenario.DbContext.FailNextSaveWith = new Microsoft.EntityFrameworkCore.DbUpdateException("FK violation");

        // Act
        var result = await CreateHandler().Handle(Unchanged(desk) with { Members = [Admin(deskAdmin.Id)] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.StaleError);
    }

    [Fact]
    public async Task Handle_ByAnAgent_FailsAndChangesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Agent(agent.Id));
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(Unchanged(desk) with { Name = "Renamed" }, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotADeskAdminError);
        desk.Name.Should().NotBe("Renamed");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByADeskAdminChangingVisibility_Fails()
    {
        // Arrange — opening a private desk would publish everything on it
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(Unchanged(desk) with { Visibility = IntakeDeskVisibility.Private },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.VisibilityNeedsAdministerError);
        desk.Visibility.Should().Be(IntakeDeskVisibility.Open);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByADeskAdminNamingAnotherDeskAdmin_Fails()
    {
        // Arrange — only the Demand Management Admin raises someone's access to running a desk
        var deskAdmin = _scenario.Employee();
        var agent = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id), Agent(agent.Id));
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(Unchanged(desk) with { Members = [Admin(deskAdmin.Id), Admin(agent.Id)] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.DeskAdminsNeedAdministerError);
        desk.RoleOf(agent.Id).Should().Be(IntakeDeskRole.Agent);
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdmin_ChangesVisibilityAndDeskAdmins()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var agent = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id), Agent(agent.Id));
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { Visibility = IntakeDeskVisibility.Private, Members = [Agent(deskAdmin.Id), Admin(agent.Id)] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.Visibility.Should().Be(IntakeDeskVisibility.Private);
        desk.RoleOf(deskAdmin.Id).Should().Be(IntakeDeskRole.Agent);
        desk.RoleOf(agent.Id).Should().Be(IntakeDeskRole.Admin);
    }

    [Fact]
    public async Task Handle_RemovingAnAgentWhoOwnsRequests_UnassignsThemOnThatDeskOnly()
    {
        // Arrange — an assignee is always a member of the request's desk
        var deskAdmin = _scenario.Employee();
        var agent = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id), Agent(agent.Id));
        var otherDesk = _scenario.DeskWithMembers(Agent(agent.Id));
        var owned = _scenario.Request(desk, _scenario.Employee());
        owned.ChangeAssignee(agent.Id, EventActor.System, _scenario.Clock.Now);
        var ownedElsewhere = _scenario.Request(otherDesk, _scenario.Employee());
        ownedElsewhere.ChangeAssignee(agent.Id, EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(Unchanged(desk) with { Members = [Admin(deskAdmin.Id)] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.RoleOf(agent.Id).Should().BeNull();
        owned.AssigneeId.Should().BeNull();
        ownedElsewhere.AssigneeId.Should().Be(agent.Id);
    }

    [Fact]
    public async Task Handle_NamingAnInactiveEmployee_Fails()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { Members = [Admin(deskAdmin.Id), Agent(_scenario.Employee(isActive: false).Id)] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Every member must be an active employee.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ToAnotherDesksName_Fails()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var other = _scenario.Desk();
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(Unchanged(desk) with { Name = other.Name }, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A desk with this name already exists.");
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReportsNotFound()
    {
        // Arrange
        var desk = new Domain.Tests.Data.IntakeDeskFaker().Generate();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(Unchanged(desk), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
    }

    [Fact]
    public async Task Handle_ByADeskAdminChoosingChannels_OffersOnlyThose()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var chosen = _scenario.Channel();
        var other = _scenario.Channel();
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { OffersAllChannels = false, ChannelIds = [chosen.Id] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.Offers(chosen).Should().BeTrue();
        desk.Offers(other).Should().BeFalse();
        desk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskChannelsChangedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NewlyChoosingAnArchivedChannel_FailsWithoutChangingTheDesk()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var archived = _scenario.Channel();
        archived.Archive(EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { OffersAllChannels = false, ChannelIds = [archived.Id] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An archived channel cannot be offered.");
        desk.OffersAllChannels.Should().BeTrue();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_KeepingAChosenChannelThatWasArchived_Succeeds()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var archived = _scenario.Channel();
        var added = _scenario.Channel();
        desk.ChangeChannels(false, [archived.Id], EventActor.System, _scenario.Clock.Now);
        desk.ClearDomainEvents();
        archived.Archive(EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { ChannelIds = [archived.Id, added.Id] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.Channels.Select(c => c.ChannelId).Should().BeEquivalentTo([archived.Id, added.Id]);
    }

    [Fact]
    public async Task Handle_LeavingTheChannelsOut_KeepsTheDesksChoice()
    {
        // Arrange — a caller that predates channels sends neither field
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var chosen = _scenario.Channel();
        desk.ChangeChannels(false, [chosen.Id], EventActor.System, _scenario.Clock.Now);
        desk.ClearDomainEvents();
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { Name = "Renamed", OffersAllChannels = null, ChannelIds = null },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.OffersAllChannels.Should().BeFalse();
        desk.Channels.Select(c => c.ChannelId).Should().Equal(chosen.Id);
        desk.DomainEvents.OfType<IntakeDeskChannelsChangedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ChoosingAnUnknownChannel_Fails()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { OffersAllChannels = false, ChannelIds = [Guid.NewGuid()] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Every channel must be a company channel.");
    }

    [Fact]
    public async Task Handle_ChoosingTagAxes_OffersOnlyThose()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var chosen = _scenario.TagCategory();
        var other = _scenario.TagCategory();
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { OffersAllTagCategories = false, TagCategoryIds = [chosen.Id] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.Offers(chosen).Should().BeTrue();
        desk.Offers(other).Should().BeFalse();
        desk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskTagCategoriesChangedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutTagAxes_KeepsTheChoiceTheDeskMade()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var chosen = _scenario.TagCategory();
        desk.ChangeTagCategories(false, [chosen.Id], EventActor.System, _scenario.Clock.Now);
        desk.ClearDomainEvents();
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(Unchanged(desk) with { Name = "Renamed" },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.OffersAllTagCategories.Should().BeFalse();
        desk.TagCategories.Should().ContainSingle(c => c.CategoryId == chosen.Id);
    }

    [Fact]
    public async Task Handle_NewlyChoosingAnInactiveTagAxis_FailsWithoutChangingTheDesk()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(Admin(deskAdmin.Id));
        var inactive = _scenario.TagCategory();
        inactive.Deactivate(EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(
            Unchanged(desk) with { OffersAllTagCategories = false, TagCategoryIds = [inactive.Id] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An inactive tag axis cannot be offered.");
        desk.OffersAllTagCategories.Should().BeTrue();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
