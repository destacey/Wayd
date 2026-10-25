using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeDeskTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));

    #region Create

    [Fact]
    public void Create_WhenValid_SetsEverySetting()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act
        var sut = IntakeDesk.Create(" IT ", " Laptops, access and internal tools ", IntakeDeskVisibility.SummaryOnly,
            isRequesterSelectable: true, isOpen: false, requesterMayAddParticipants: false,
            [new(adminId, IntakeDeskRole.Admin), new(agentId, IntakeDeskRole.Agent)],
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Name.Should().Be("IT");
        sut.Description.Should().Be("Laptops, access and internal tools");
        sut.Visibility.Should().Be(IntakeDeskVisibility.SummaryOnly);
        sut.IsRequesterSelectable.Should().BeTrue();
        sut.IsOpen.Should().BeFalse();
        sut.RequesterMayAddParticipants.Should().BeFalse();
        sut.Members.Should().BeEquivalentTo(
        [
            new { DeskId = sut.Id, EmployeeId = adminId, Role = IntakeDeskRole.Admin },
            new { DeskId = sut.Id, EmployeeId = agentId, Role = IntakeDeskRole.Agent },
        ]);
    }

    [Fact]
    public void Create_WithARepeatedMember_NamesThemOnce()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var sut = IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [new(agentId, IntakeDeskRole.Agent), new(agentId, IntakeDeskRole.Agent)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Members.Should().ContainSingle();
    }

    [Fact]
    public void Create_WithAnEmployeeInTwoRoles_Throws()
    {
        // Arrange
        var employeeId = Guid.NewGuid();

        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [new(employeeId, IntakeDeskRole.Admin), new(employeeId, IntakeDeskRole.Agent)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnEmptyMemberId_Throws()
    {
        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [new(Guid.Empty, IntakeDeskRole.Agent)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnUndefinedRole_Throws()
    {
        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [new(Guid.NewGuid(), (IntakeDeskRole)99)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("  ", "Internal tools")]
    [InlineData("IT", "  ")]
    public void Create_WithABlankNameOrDescription_Throws(string name, string description)
    {
        // Act
        Action act = () => IntakeDesk.Create(name, description, IntakeDeskVisibility.Open, true, true, true,
            [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnUndefinedVisibility_Throws()
    {
        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", (IntakeDeskVisibility)99, true, true, true,
            [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RaisesCreatedEventOnlyOnceKeyed_WithTheDeskAsCreated()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var timestamp = _dateTimeProvider.Now;

        // Act
        var sut = IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Private, false, true, false,
            [new(adminId, IntakeDeskRole.Admin)], EventActor.System, timestamp);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.Should().ContainSingle().Which();

        var created = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskCreatedEvent>().Subject;
        created.Id.Should().Be(sut.Id);
        created.Name.Should().Be("IT");
        created.Description.Should().Be("Internal tools");
        created.Visibility.Should().Be(IntakeDeskVisibility.Private);
        created.IsRequesterSelectable.Should().BeFalse();
        created.IsOpen.Should().BeTrue();
        created.RequesterMayAddParticipants.Should().BeFalse();
        created.Members.Should().Equal(new IntakeDeskMemberDetails(adminId, IntakeDeskRole.Admin));
        created.Timestamp.Should().Be(timestamp);
    }

    #endregion Create

    #region RoleOf

    [Theory]
    [InlineData(IntakeDeskRole.Admin)]
    [InlineData(IntakeDeskRole.Agent)]
    public void RoleOf_ForAMember_IsTheirRole(IntakeDeskRole role)
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var sut = new IntakeDeskFaker()
            .WithMembers(new(Guid.NewGuid(), IntakeDeskRole.Agent), new(employeeId, role))
            .Generate();

        // Act
        var result = sut.RoleOf(employeeId);

        // Assert
        result.Should().Be(role);
    }

    [Fact]
    public void RoleOf_ForSomeoneNotNamed_IsNull()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithAgents(Guid.NewGuid()).Generate();

        // Act
        var result = sut.RoleOf(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion RoleOf
}
