using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The team's current operating model is projected with its optional cadence, a complex property only a real
/// provider translates inside the nested projection.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2026, 1, 5);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private async Task<Team> SeedKanbanTeam(ReportingCadence? cadence, CancellationToken cancellationToken)
    {
        var code = new TeamCode($"K{Random.Shared.Next(10_000, 99_999)}");
        var team = Team.Create($"Kanban {code.Value}", code, null, ActiveDate, Methodology.Kanban, SizingMethod.Count,
            "UTC", 1, WorkingWeek.MondayToFriday, EventActor.System, SqlServerDbContextFixture.FixedNow);
        if (cadence is not null)
        {
            var model = team.OperatingModels.Single();
            team.CorrectOperatingModel(model.Id, Methodology.Kanban, SizingMethod.Count, "America/Chicago", 1, WorkingWeek.MondayToFriday,
                null, cadence, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        }

        await using var seed = _fixture.CreateContext();
        seed.Teams.Add(team);
        await seed.SaveChangesAsync(cancellationToken);
        return team;
    }

    private static GetTeamQueryHandler Handler(IOrganizationDbContext context) =>
        new(context, NullLogger<GetTeamQueryHandler>.Instance,
            Mock.Of<IDateTimeProvider>(p => p.Today == ActiveDate.PlusDays(10)));

    [Fact]
    public async Task Handle_ReturnsTheCurrentModelsCadence()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var cadence = ReportingCadence.Create(2, ActiveDate).Value;
        var team = await SeedKanbanTeam(cadence, cancellationToken);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await Handler(context).Handle(new GetTeamQuery(team.Id), cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.OperatingModel.CadenceWeeks.Should().Be(2);
        result.OperatingModel.CadenceAnchor.Should().Be(ActiveDate);
        result.OperatingModel.TimeZone.Should().Be("America/Chicago");
    }

    [Fact]
    public async Task Handle_WithNoCadence_ReturnsNone()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var team = await SeedKanbanTeam(null, cancellationToken);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await Handler(context).Handle(new GetTeamQuery(team.Id), cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.OperatingModel.CadenceWeeks.Should().BeNull();
        result.OperatingModel.CadenceAnchor.Should().BeNull();
    }
}
