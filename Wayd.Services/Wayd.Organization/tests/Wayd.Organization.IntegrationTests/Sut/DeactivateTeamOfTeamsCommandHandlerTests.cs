using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Tests the team of teams deactivation. A team of teams cannot be deactivated before its child memberships end,
/// a rule the handler can apply only if it loads them; a fake context reads them as empty and every date passes.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeactivateTeamOfTeamsCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate Start = ActiveDate.PlusDays(10);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_WhenAChildMembershipIsStillOpen_RefusesTheDeactivation()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        Guid parentId;
        await using (var seed = _fixture.CreateContext())
        {
            var parent = NewTeamOfTeams("Deactivate Parent");
            var child = NewTeamOfTeams("Deactivate Child");
            await seed.TeamOfTeams.AddRangeAsync([parent, child], cancellationToken);
            await seed.SaveChangesAsync(cancellationToken);
            child.AddTeamMembership(parent, new MembershipDateRange(Start, null), EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await seed.SaveChangesAsync(cancellationToken);
            parentId = parent.Id;
        }

        await using var context = _fixture.CreateContext();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        var handler = new DeactivateTeamOfTeamsCommandHandler(context, currentUser.Object, NullLogger<DeactivateTeamOfTeamsCommand>.Instance, clock.Object);

        // Act
        var result = await handler.Handle(new DeactivateTeamOfTeamsCommand(parentId, Start.PlusDays(30)), cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The inactive date must be on or after the end date of the last child team membership.");
        await using var verify = _fixture.CreateContext();
        (await verify.TeamOfTeams.SingleAsync(t => t.Id == parentId, cancellationToken)).IsActive.Should().BeTrue();
    }

    private static TeamOfTeams NewTeamOfTeams(string name)
        => TeamOfTeams.Create($"{name} {Guid.NewGuid():N}"[..30], new TeamCode($"D{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"), null, ActiveDate, "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);
}
