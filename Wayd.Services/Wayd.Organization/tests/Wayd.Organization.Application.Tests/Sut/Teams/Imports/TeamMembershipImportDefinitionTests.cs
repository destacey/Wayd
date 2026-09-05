using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Imports;

public sealed class TeamMembershipImportDefinitionTests : IDisposable
{
    private const int AddPass = 0;
    private const int SyncPass = 1;

    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate MembershipStart = new(2024, 6, 1);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamMembershipImportDefinition _definition;

    public TeamMembershipImportDefinitionTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));
        _definition = new TeamMembershipImportDefinition(_dbContext, clock, new ImportPayloadSerializer());
    }

    private Team SeedTeam(string code)
    {
        var team = new TeamFaker().WithCode(new TeamCode(code)).WithActiveDate(ActiveDate).AsActive().Generate();
        _dbContext.AddTeam(team);
        return team;
    }

    private TeamOfTeams SeedTeamOfTeams(string code)
    {
        var tot = new TeamOfTeamsFaker().WithCode(new TeamCode(code)).WithActiveDate(ActiveDate).AsActive().Generate();
        _dbContext.AddTeamOfTeams(tot);
        return tot;
    }

    private ImportProcessRow[] Rows(params (string Child, string Parent)[] edges) =>
        [.. edges.Select((e, i) => ImportProcessRow.Create(
            $"r{i + 1}", i + 1,
            _definition.SerializeRow(new ImportTeamMembershipDto(e.Child, e.Parent, MembershipStart, End: null))))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> RunPass(int passIndex, ImportProcessRow[] rows) =>
        _definition.ExecutePass(Guid.CreateVersion7(), passIndex, rows, isFinalChunk: true, TestContext.Current.CancellationToken);

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public void Definition_IsAtomicAndCannotBeChunked()
    {
        // Arrange & Act
        var passes = _definition.Passes;

        // Assert — half an imported hierarchy is worse than none, and the cycle check needs the whole file
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        passes.Select(p => p.Name).Should().Equal("AddMemberships", "SyncGraphEdges");
        passes.Should().AllSatisfy(p => p.Scope.Should().Be(ImportPassScope.WholeSet));
    }

    [Fact]
    public async Task AddMemberships_PlacesATeamUnderATeamOfTeams()
    {
        // Arrange
        SeedTeam("TEAM");
        SeedTeamOfTeams("ART");
        var rows = Rows(("TEAM", "ART"));

        // Act
        var result = await RunPass(AddPass, rows);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Single().Failed.Should().BeFalse();
        result.Value.Rows.Single().CreatedEntityId.Should().NotBeNull();
    }

    [Fact]
    public async Task AddMemberships_BuildsAThreeTierHierarchyInOneFile()
    {
        // Arrange — a Team of Teams may itself be a child, which is what makes three tiers possible
        SeedTeam("TEAM");
        SeedTeamOfTeams("ART");
        SeedTeamOfTeams("VS");
        var rows = Rows(("TEAM", "ART"), ("ART", "VS"));

        // Act
        var result = await RunPass(AddPass, rows);

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
    }

    [Fact]
    public async Task AddMemberships_RejectsARowNamingATeamThatDoesNotExist()
    {
        // Arrange
        SeedTeamOfTeams("ART");
        var rows = Rows(("MISSING", "ART"));

        // Act
        var result = await RunPass(AddPass, rows);

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("MISSING");
    }

    [Fact]
    public async Task AddMemberships_RejectsARowWhoseParentIsAPlainTeam()
    {
        // Arrange
        SeedTeam("CHILD");
        SeedTeam("PARENT");
        var rows = Rows(("CHILD", "PARENT"));

        // Act
        var result = await RunPass(AddPass, rows);

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Team of Teams");
    }

    [Fact]
    public async Task AddMemberships_AddsNothingWhenAnyRowIsRejected()
    {
        // Arrange — one good row and one naming a missing team
        SeedTeam("TEAM");
        SeedTeamOfTeams("ART");
        var rows = Rows(("TEAM", "ART"), ("MISSING", "ART"));

        // Act
        var result = await RunPass(AddPass, rows);

        // Assert — the contract an atomic definition owes the runner: validated before anything mutated, so
        // failing the run leaves nothing to undo
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        result.Value.Rows.Single(r => r.ImportId == "r1").CreatedEntityId.Should().BeNull();

        var team = _dbContext.BaseTeams.Single(t => t.Code == new TeamCode("TEAM"));
        team.ParentMemberships.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncGraphEdges_MirrorsEachNewEdgeAfterTheRelationalSave()
    {
        // Arrange
        SeedTeam("TEAM");
        SeedTeamOfTeams("ART");
        var rows = Rows(("TEAM", "ART"));

        var addResult = await RunPass(AddPass, rows);
        // The runner records the created id and saves between passes; stand in for both.
        rows[0].RecordCreatedEntity(addResult.Value.Rows.Single().CreatedEntityId!.Value);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await RunPass(SyncPass, rows);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.UpsertTeamMembershipEdgeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task SyncGraphEdges_DoesNothingWhenTheAddPassCreatedNothing()
    {
        // Arrange — every row was rejected, so there is no edge to mirror
        SeedTeamOfTeams("ART");
        var rows = Rows(("MISSING", "ART"));
        await RunPass(AddPass, rows);

        // Act
        var result = await RunPass(SyncPass, rows);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.UpsertTeamMembershipEdgeCallCount.Should().Be(0);
    }
}
