using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;
using Wayd.Tests.Shared;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="TeamMembershipImportDefinition"/> against a real SQL Server container.
/// </summary>
/// <remarks>
/// This import is the one whose correctness argument only a real provider can demonstrate. It is declared
/// <see cref="ImportPassScope.WholeSet"/> because it loads every team tracked with its memberships so EF's
/// relationship fixup keeps both ends of each new edge consistent — which is what the domain's cycle check
/// reads. An in-memory fake has no fixup at all, so against one the tests pass whether or not that mechanism
/// works. These do not.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class TeamMembershipImportDefinitionTests
{
    private const int AddPass = 0;

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

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("user-1");

        return new TeamMembershipImportDefinition(context, dateTimeProvider.Object, currentUser.Object, new ImportPayloadSerializer());
    }

    /// <summary>
    /// Seeds through the domain factories, the way the create handlers do, so the rows are in the shape the
    /// application actually produces.
    /// </summary>
    private async Task SeedHierarchyTeams(CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var actor = EventActor.System;
        var now = SqlServerDbContextFixture.FixedNow;

        var team = Team.Create("Payments", new TeamCode("TEAM"), null, ActiveDate,
            Methodology.Kanban, SizingMethod.Count, "UTC", 1, WorkingWeek.MondayToFriday, actor, now);
        var art = TeamOfTeams.Create("Payments ART", new TeamCode("ART"), null, ActiveDate, "UTC", actor, now);
        var valueStream = TeamOfTeams.Create("Payments VS", new TeamCode("VS"), null, ActiveDate, "UTC", actor, now);

        await context.Teams.AddAsync(team, cancellationToken);
        await context.TeamOfTeams.AddAsync(art, cancellationToken);
        await context.TeamOfTeams.AddAsync(valueStream, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

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
    public async Task AddMemberships_ResolvesTeamsByCode()
    {
        // Arrange
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

    /// <summary>
    /// The intermediate team is not in the file, so only loading teams beyond the file's lets the cycle
    /// check see that the new parent already sits two levels below the child.
    /// </summary>
    [Fact]
    public async Task AddMemberships_RefusesACycleThroughATeamTheFileDoesNotName()
    {
        // Arrange — ART under VS, VS under PORT, already saved
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        await SeedHierarchyTeams(cancellationToken);

        await using (var seedContext = _fixture.CreateContext())
        {
            var art = await seedContext.TeamOfTeams.SingleAsync(t => t.Code == new TeamCode("ART"), cancellationToken);
            var valueStream = await seedContext.TeamOfTeams.SingleAsync(t => t.Code == new TeamCode("VS"), cancellationToken);
            var portfolio = TeamOfTeams.Create("Payments Portfolio", new TeamCode("PORT"), null, ActiveDate, "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);
            await seedContext.TeamOfTeams.AddAsync(portfolio, cancellationToken);

            var range = new MembershipDateRange(MembershipStart, null);
            art.AddTeamMembership(valueStream, range, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            valueStream.AddTeamMembership(portfolio, range, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("PORT", "ART"));

        // Act
        var result = await RunPass(context, definition, AddPass, rows, cancellationToken);

        // Assert
        result.Rows.Single().Failed.Should().BeTrue();

        await using var assertContext = _fixture.CreateContext();
        var stored = await assertContext.BaseTeams
            .Include(t => t.ParentMemberships)
            .SingleAsync(t => t.Code == new TeamCode("PORT"), cancellationToken);

        stored.ParentMemberships.Should().BeEmpty();
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
    public async Task ARunLetsGoOfWhatItAppliedOnceItIsSaved()
    {
        // Arrange — every module interface resolves to the one context in the message, so a run that held
        // on to what it created would grow with the file rather than with the chunk
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        await SeedHierarchyTeams(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition, ("TEAM", "ART"));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert — the run and its rows stay, because the runner is still writing to them
        process.Status.Should().Be(ImportProcessStatus.Succeeded);
        context.ChangeTracker.Entries()
            .Select(e => e.Entity)
            .Should().AllSatisfy(e => e.Should().Match(x => x is ImportProcess || x is ImportProcessRow));
    }
}
