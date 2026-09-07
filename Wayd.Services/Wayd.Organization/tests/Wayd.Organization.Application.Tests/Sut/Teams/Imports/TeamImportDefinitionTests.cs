using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Imports;

public sealed class TeamImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;
    private const int SyncPass = 1;

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamImportDefinition _definition;

    public TeamImportDefinitionTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _definition = new TeamImportDefinition(
            _dbContext, clock, currentUser.Object, new ImportPayloadSerializer());
    }

    public void Dispose() => _dbContext.Dispose();

    private static ImportTeamDto Row(TeamType type, string name, string code) =>
        new(type, name, new TeamCode(code), Description: null, ActiveDate: new LocalDate(2026, 1, 1));

    private static ImportTeamDto InactiveRow(
        TeamType type, string name, string code, LocalDate activeDate, LocalDate inactiveDate) =>
        new(type, name, new TeamCode(code), Description: null, ActiveDate: activeDate,
            IsActive: false, InactiveDate: inactiveDate);

    private ImportProcessRow[] Rows(params ImportTeamDto[] teams) =>
        [.. teams.Select((t, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(t)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(
        int passIndex, ImportProcessRow[] rows) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), passIndex, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicWithTwoPasses()
    {
        // Arrange & Act
        var passes = _definition.Passes;

        // Assert — creating a team replicates it into PPM, Planning and Work, so a half-applied file
        // leaves those areas holding half an organization
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        passes.Select(p => p.Name).Should().Equal("CreateTeams", "SyncGraphNodes");
    }

    [Fact]
    public async Task CreateTeams_CreatesEachKindByType()
    {
        // Arrange
        var rows = Rows(
            Row(TeamType.Team, "Payments Core", "PAY"),
            Row(TeamType.Team, "Platform Enablement", "PLAT"),
            Row(TeamType.TeamOfTeams, "Payments Group", "PAYG"));

        // Act
        var result = await Run(CreatePass, rows);

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        var teams = await _dbContext.Teams.ToListAsync(TestContext.Current.CancellationToken);
        var teamsOfTeams = await _dbContext.TeamOfTeams.ToListAsync(TestContext.Current.CancellationToken);

        teams.Select(t => t.Code.Value).Should().BeEquivalentTo(["PAY", "PLAT"]);
        teamsOfTeams.Single().Code.Value.Should().Be("PAYG");
    }

    [Fact]
    public async Task CreateTeams_ReportsTheTeamItCreatedAgainstTheRow()
    {
        // Arrange & Act — the created id is the durable link back, since the import id is not stored on it
        var result = await Run(CreatePass, Rows(Row(TeamType.Team, "Payments Core", "PAY")));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.CreatedEntityId.Should().NotBeNull();
        _dbContext.Teams.Single().Id.Should().Be(outcome.CreatedEntityId!.Value);
    }

    [Fact]
    public async Task CreateTeams_ImportsARetiredTeamAsInactive()
    {
        // Arrange — created active, which is the only way the domain allows, then deactivated through the
        // same behaviour the UI uses so the event fires
        var rows = Rows(InactiveRow(
            TeamType.Team, "Legacy Payments", "LEG",
            new LocalDate(2024, 1, 1), new LocalDate(2025, 6, 30)));

        // Act
        var result = await Run(CreatePass, rows);

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var team = _dbContext.Teams.Single();
        team.IsActive.Should().BeFalse();
        team.InactiveDate.Should().Be(new LocalDate(2025, 6, 30));
    }

    [Fact]
    public async Task CreateTeams_ImportsAMixOfActiveAndRetired()
    {
        // Arrange
        var rows = Rows(
            Row(TeamType.Team, "Payments Core", "PAY"),
            InactiveRow(TeamType.Team, "Legacy Payments", "LEG",
                new LocalDate(2024, 1, 1), new LocalDate(2025, 6, 30)));

        // Act
        await Run(CreatePass, rows);

        // Assert
        var teams = await _dbContext.Teams.ToListAsync(TestContext.Current.CancellationToken);
        teams.Single(t => t.Code.Value == "PAY").IsActive.Should().BeTrue();
        teams.Single(t => t.Code.Value == "LEG").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTeams_RejectsARowWhoseCodeIsAlreadyTaken()
    {
        // Arrange — a unique index, so this used to surface as a constraint violation when the batch saved
        var existing = Team.Create(
            "Existing", new TeamCode("PAY"), null, new LocalDate(2024, 1, 1),
            Wayd.Organization.Domain.Enums.Methodology.Kanban,
            Wayd.Organization.Domain.Enums.SizingMethod.Count,
            Wayd.Common.Domain.Events.EventActor.System, Instant.FromUtc(2024, 1, 1, 0, 0));
        _dbContext.AddTeam(existing);

        // Act
        var result = await Run(CreatePass, Rows(Row(TeamType.Team, "Payments Core", "PAY")));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("PAY");
    }

    [Fact]
    public async Task SyncGraphNodes_DoesNothingWhenNoRowCreatedATeam()
    {
        // Arrange — every row was rejected, so there is no node to mirror. That it writes the right nodes
        // when there are any is asserted against a real provider: this fake's BaseTeams is a separate list
        // from Teams, where EF's is the TPH base set, so the lookup the pass does finds nothing here.
        var rows = Rows(Row(TeamType.Team, "Payments Core", "PAY"));

        // Act
        var result = await Run(SyncPass, rows);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.UpsertTeamNodeCallCount.Should().Be(0);
    }
}
