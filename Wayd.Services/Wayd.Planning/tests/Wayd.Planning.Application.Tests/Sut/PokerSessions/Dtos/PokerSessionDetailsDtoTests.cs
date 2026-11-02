using Mapster;
using NodaTime;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Dtos;

/// <summary>
/// Covers how a session's facilitators, invites and members reach the details DTO.
/// </summary>
public class PokerSessionDetailsDtoTests
{
    private static readonly TypeAdapterConfig Config = MapsterTestConfiguration.Config;

    [Fact]
    public void Mapping_ShouldCarryAccessAndMembers()
    {
        // Arrange
        var invitedEmployeeId = Guid.NewGuid();
        var session = new PokerSessionFaker()
            .WithVisibility(Visibility.Private)
            .WithInvitedEmployees(invitedEmployeeId)
            .Generate();
        var early = PokerSessionActors.Voter("early");
        var late = PokerSessionActors.Voter("late");
        session.SetInvitees([invitedEmployeeId, early.EmployeeId, late.EmployeeId], [], PokerSessionActors.Facilitator);
        session.Join(PokerSessionMemberMode.Observer, late, Instant.FromUtc(2026, 1, 15, 11, 0));
        session.Join(PokerSessionMemberMode.Participant, early, Instant.FromUtc(2026, 1, 15, 10, 0));
        session.WithEmployeesLinked();

        // Act
        var dto = session.Adapt<PokerSessionDetailsDto>(Config);

        // Assert
        dto.Visibility.Should().Be(Visibility.Private);
        dto.Facilitators.Select(f => f.Id).Should().Equal(PokerSessionActors.FacilitatorEmployeeId);
        dto.InvitedEmployees.Select(e => e.Id).Should().BeEquivalentTo([invitedEmployeeId, early.EmployeeId, late.EmployeeId]);
        dto.Members.Select(m => m.Employee.Id).Should().Equal(early.EmployeeId, late.EmployeeId);
        dto.Members.Select(m => m.Mode).Should().Equal(PokerSessionMemberMode.Participant, PokerSessionMemberMode.Observer);
        dto.CanManage.Should().BeFalse();
    }
}
