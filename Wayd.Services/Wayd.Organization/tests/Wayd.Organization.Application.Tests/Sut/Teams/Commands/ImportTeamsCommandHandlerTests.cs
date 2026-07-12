using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class ImportTeamsCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TestingDateTimeProvider _dateTimeProvider =
        new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

    private ImportTeamsCommandHandler CreateHandler() =>
        new(_dbContext, _dateTimeProvider, NullLogger<ImportTeamsCommandHandler>.Instance);

    private static ImportTeamDto Row(TeamType type, string name, string code) =>
        new(type, name, new TeamCode(code), Description: null, ActiveDate: new LocalDate(2026, 1, 1));

    private static ImportTeamDto InactiveRow(TeamType type, string name, string code, LocalDate activeDate, LocalDate inactiveDate) =>
        new(type, name, new TeamCode(code), Description: null, ActiveDate: activeDate, IsActive: false, InactiveDate: inactiveDate);

    [Fact]
    public async Task Handle_CreatesTeamsAndTeamsOfTeams_ByType()
    {
        // Arrange
        var command = new ImportTeamsCommand(
        [
            Row(TeamType.Team, "Payments Core", "PAY"),
            Row(TeamType.Team, "Platform Enablement", "PLAT"),
            Row(TeamType.TeamOfTeams, "Payments Group", "PAYG"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var teams = await _dbContext.Teams.ToListAsync(TestContext.Current.CancellationToken);
        var teamsOfTeams = await _dbContext.TeamOfTeams.ToListAsync(TestContext.Current.CancellationToken);
        teams.Should().HaveCount(2);
        teamsOfTeams.Should().HaveCount(1);
        teams.Select(t => t.Code.Value).Should().BeEquivalentTo(["PAY", "PLAT"]);
        teamsOfTeams.Single().Code.Value.Should().Be("PAYG");
    }

    [Fact]
    public async Task Handle_SavesOnce_ForTheWholeBatch()
    {
        // Arrange
        var command = new ImportTeamsCommand(
        [
            Row(TeamType.Team, "Team A", "TA"),
            Row(TeamType.Team, "Team B", "TB"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert — a single SaveChanges dispatches all creation events in one transaction.
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SyncsEachNewTeamIntoTheGraph_Once()
    {
        // Arrange
        var command = new ImportTeamsCommand(
        [
            Row(TeamType.Team, "Team A", "TA"),
            Row(TeamType.Team, "Team B", "TB"),
            Row(TeamType.TeamOfTeams, "Group", "GRP"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.UpsertTeamNodeCallCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ImportsRetiredTeam_AsInactiveWithInactiveDate()
    {
        // Arrange — a team that was active 2020-2023 (a migration / historical fixture scenario).
        var activeDate = new LocalDate(2020, 1, 1);
        var inactiveDate = new LocalDate(2023, 6, 30);
        var command = new ImportTeamsCommand([InactiveRow(TeamType.Team, "Legacy Team", "LEG", activeDate, inactiveDate)]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert — created then deactivated through the domain, so both flag and date are set.
        result.IsSuccess.Should().BeTrue();
        var team = (await _dbContext.Teams.ToListAsync(TestContext.Current.CancellationToken)).Single();
        team.IsActive.Should().BeFalse();
        team.InactiveDate.Should().Be(inactiveDate);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ImportsMixOfActiveAndRetiredTeams()
    {
        // Arrange
        var command = new ImportTeamsCommand(
        [
            Row(TeamType.Team, "Current Team", "CUR"),
            InactiveRow(TeamType.TeamOfTeams, "Old Group", "OLDG", new LocalDate(2019, 1, 1), new LocalDate(2022, 12, 31)),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var team = (await _dbContext.Teams.ToListAsync(TestContext.Current.CancellationToken)).Single();
        var group = (await _dbContext.TeamOfTeams.ToListAsync(TestContext.Current.CancellationToken)).Single();
        team.IsActive.Should().BeTrue();
        group.IsActive.Should().BeFalse();
        group.InactiveDate.Should().Be(new LocalDate(2022, 12, 31));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
