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
    public void Mapping_ShouldListEveryoneWhoJoinedInJoinOrder()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        var first = PokerSessionActors.Voter("first");
        var second = PokerSessionActors.Voter("second");
        var nonVoter = PokerSessionActors.Voter("non-voter");
        var now = Instant.FromUtc(2026, 1, 15, 10, 0);
        session.Join(true, first, now);
        session.Join(true, second, now.Plus(Duration.FromMinutes(1)));
        session.Join(false, nonVoter, now.Plus(Duration.FromMinutes(2)));
        session.WithEmployeesLinked();

        // Act
        var dto = session.Adapt<PokerSessionListDto>(Config);

        // Assert
        dto.Participants.Select(p => p.Id).Should().Equal(first.EmployeeId, second.EmployeeId, nonVoter.EmployeeId);
        dto.Facilitators.Select(f => f.Id).Should().Equal(PokerSessionActors.FacilitatorEmployeeId);
    }
}
