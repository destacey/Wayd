using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Organization.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Application.Teams.Models;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="TeamMembershipImportDefinition"/> against a real SQL Server container.
/// </summary>
/// <remarks>
/// This import is the one whose correctness argument only a real provider can demonstrate. It is declared
/// <see cref="ImportPassScope.WholeSet"/> because it loads every referenced team tracked with its
/// memberships so EF's relationship fixup keeps both ends of each new edge consistent — which is what the
/// domain's cycle check reads. An in-memory fake has no fixup at all, so against one the tests pass whether
/// or not that mechanism works. These do not.
/// <para>
/// They also cover the two things a fake cannot see: that the <c>TeamCode</c> value-converter lookup
/// translates to SQL, and that the graph-edge sync actually writes a row to the SQL graph table.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class TeamMembershipImportDefinitionTests
{
    private const int AddPass = 0;
    private const int SyncPass = 1;

    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate MembershipStart = new(2024, 6, 1);

    private readonly SqlServerDbContextFixture _fixture;

    public TeamMembershipImportDefinitionTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static TeamMembershipImportDefinition CreateDefinition(Wayd.Infrastructure.Persistence.Context.WaydDbContext context)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        dateTimeProvider.SetupGet(d => d.Today).Returns(SqlServerDbContextFixture.FixedNow.InUtc().Date);

        return new TeamMembershipImportDefinition(context, dateTimeProvider.Object, new ImportPayloadSerializer());
    }

    /// <summary>
    /// Seeds through the domain factories and the graph-node upsert, the way the create handlers do, so the
    /// rows are in the shape the application actually produces.
    /// </summary>
    private async Task SeedHierarchyTeams(CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var actor = EventActor.System;
        var now = SqlServerDbContextFixture.FixedNow;

        var team = Team.Create("Payments", new TeamCode("TEAM"), null, ActiveDate,
            Methodology.Kanban, SizingMethod.Count, actor, now);
        var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, actor, now);
        var valueStream = TeamOfTeams.Create("Payments VS", new TeamCode("VS"), null, ActiveDate, actor, now);

        await context.Teams.AddAsync(team, cancellationToken);
        await context.TeamOfTeams.AddAsync(art, cancellationToken);
        await context.TeamOfTeams.AddAsync(valueStream, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Edges reference nodes, so the graph rows have to exist before the sync pass runs.
        await context.UpsertTeamNode(TeamNode.From(team), cancellationToken);
        await context.UpsertTeamNode(TeamNode.From(art), cancellationToken);
        await context.UpsertTeamNode(TeamNode.From(valueStream), cancellationToken);
    }

    private static ImportProcessRow[] Rows(TeamMembershipImportDefinition definition, params (string Child, string Parent)[] edges) =>
        [.. edges.Select((e, i) => ImportProcessRow.Create(
            $"r{i + 1}", i + 1,
            definition.SerializeRow(new ImportTeamMembershipDto(e.Child, e.Parent, MembershipStart, End: null))))];

    /// <summary>Runs a pass and saves, the way the runner does between passes.</summary>
    private static async Task<ImportPassResult> RunPass(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context,
        TeamMembershipImportDefinition definition,
        int passIndex,
        ImportProcessRow[] rows,
        CancellationToken cancellationToken)
    {
        var result = await definition.ExecutePass(
            Guid.CreateVersion7(), passIndex, rows, isFinalChunk: true, cancellationToken);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await context.SaveChangesAsync(cancellationToken);

        // The runner records each created id before the next pass; stand in for that.
        foreach (var outcome in result.Value.Rows.Where(r => r.CreatedEntityId is not null))
        {
            rows.Single(row => row.ImportId == outcome.ImportId).RecordCreatedEntity(outcome.CreatedEntityId!.Value);
        }

        return result.Value;
    }

    /// <summary>
    /// Runs the real runner over a real import process, so the atomic guarantee is exercised end to end
    /// rather than simulated. Returns the finished run.
    /// </summary>
    private async Task<ImportProcess> RunImport(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context,
        TeamMembershipImportDefinition definition,
        ImportProcessRow[] rows,
        CancellationToken cancellationToken)
    {
        var process = ImportProcess.Create(
            TeamMembershipImportDefinition.ImportKey, "user-1", null, rows, SqlServerDbContextFixture.FixedNow);

        await context.ImportProcesses.AddAsync(process, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        var handler = new RunImportProcessCommandHandler(
            context,
            new ImportDefinitionRegistry([definition]),
            clock.Object,
            NullLogger<RunImportProcessCommandHandler>.Instance);

        var result = await handler.Handle(new RunImportProcessCommand(process.Id), cancellationToken);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        return process;
    }

    [Fact]
    public async Task AddMemberships_ResolvesTeamsByCodeThroughTheValueConverter()
    {
        // Arrange — TeamCode is a HasConversion value object; comparing against a member of it rather than
        // the property itself does not translate, and throws only against a real provider
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        await SeedHierarchyTeams(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("TEAM", "ART"));

        // Act
        var result = await RunPass(context, definition, AddPass, rows, cancellationToken);

        // Assert
        result.Rows.Single().Failed.Should().BeFalse();

        await using var assertContext = _fixture.CreateContext();
        var team = await assertContext.BaseTeams
            .Include(t => t.ParentMemberships)
            .SingleAsync(t => t.Code == new TeamCode("TEAM"), cancellationToken);

        team.ParentMemberships.Should().ContainSingle();
    }

    [Fact]
    public async Task AddMemberships_BuildsAThreeTierHierarchyFromOneFile()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        await SeedHierarchyTeams(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("TEAM", "ART"), ("ART", "VS"));

        // Act
        var result = await RunPass(context, definition, AddPass, rows, cancellationToken);

        // Assert
        result.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        await using var assertContext = _fixture.CreateContext();
        var art = await assertContext.BaseTeams
            .Include(t => t.ParentMemberships)
            .SingleAsync(t => t.Code == new TeamCode("ART"), cancellationToken);

        // The ART is both a parent and a child, which is what makes three tiers possible.
        art.ParentMemberships.Should().ContainSingle();
    }

    /// <summary>
    /// The reason this import is WholeSet. Neither row forms a cycle on its own; together they do, and the
    /// domain can only see that because both edges are tracked on the same loaded graph. Chunk the file and
    /// the check goes blind — this is the test that would fail if anyone made the pass chunkable.
    /// </summary>
    [Fact]
    public async Task AtomicRun_AppliesNothingWhenTwoRowsTogetherFormACycle()
    {
        // Arrange — neither edge is a cycle alone; together they close a loop, which the domain can only
        // see because both are on the same loaded graph
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        await SeedHierarchyTeams(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("ART", "VS"), ("VS", "ART"));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert — the run is failed and the first edge, staged before the cycle was found, is discarded
        process.Status.Should().Be(ImportProcessStatus.Failed);

        await using var assertContext = _fixture.CreateContext();
        var art = await assertContext.BaseTeams
            .Include(t => t.ParentMemberships)
            .SingleAsync(t => t.Code == new TeamCode("ART"), cancellationToken);
        var valueStream = await assertContext.BaseTeams
            .Include(t => t.ParentMemberships)
            .SingleAsync(t => t.Code == new TeamCode("VS"), cancellationToken);

        art.ParentMemberships.Should().BeEmpty();
        valueStream.ParentMemberships.Should().BeEmpty();
    }

    [Fact]
    public async Task AddMemberships_PersistsNothingWhenAnyRowIsRejected()
    {
        // Arrange — one row that would succeed, one naming a team that does not exist
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        await SeedHierarchyTeams(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("TEAM", "ART"), ("MISSING", "ART"));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert
        process.Status.Should().Be(ImportProcessStatus.Failed);

        await using var assertContext = _fixture.CreateContext();
        var team = await assertContext.BaseTeams
            .Include(t => t.ParentMemberships)
            .SingleAsync(t => t.Code == new TeamCode("TEAM"), cancellationToken);

        team.ParentMemberships.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncGraphEdges_WritesTheEdgeToTheGraphTable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        await SeedHierarchyTeams(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("TEAM", "ART"));
        await RunPass(context, definition, AddPass, rows, cancellationToken);

        // Act
        await RunPass(context, definition, SyncPass, rows, cancellationToken);

        // Assert — the graph write is raw SQL against a SQL Server graph table, so only a real provider
        // shows it happened at all
        await using var assertContext = _fixture.CreateContext();
        var edges = await assertContext.Set<TeamMembershipEdge>().CountAsync(cancellationToken);
        edges.Should().Be(1);
    }
}
