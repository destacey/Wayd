using Mapster;
using NodaTime;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Dtos;

public class PokerSessionListDtoTests
{
    private static readonly TypeAdapterConfig Config = MapsterTestConfiguration.Config;

    [Fact]
    public void Mapping_ShouldListParticipantsIncludingThoseWhoLeftButNotObservers()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        var stayed = PokerSessionActors.Voter("stayed");
        var left = PokerSessionActors.Voter("left");
        var observer = PokerSessionActors.Voter("observer");
        var now = Instant.FromUtc(2026, 1, 15, 10, 0);
        session.Join(PokerSessionMemberMode.Participant, stayed, now);
        session.Join(PokerSessionMemberMode.Participant, left, now.Plus(Duration.FromMinutes(1)));
        session.Leave(left, now.Plus(Duration.FromMinutes(2)));
        session.Join(PokerSessionMemberMode.Observer, observer, now);
        session.WithEmployeesLinked();

        // Act
        var dto = session.Adapt<PokerSessionListDto>(Config);

        // Assert
        dto.Participants.Select(p => p.Id).Should().Equal(stayed.EmployeeId, left.EmployeeId);
        dto.Facilitators.Select(f => f.Id).Should().Equal(PokerSessionActors.FacilitatorEmployeeId);
    }
}
