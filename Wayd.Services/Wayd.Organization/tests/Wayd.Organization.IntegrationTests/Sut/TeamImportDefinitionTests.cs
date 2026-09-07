using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Application.Teams.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The team import against a real SQL Server container.
/// </summary>
/// <remarks>
/// Two things only a real provider shows. The graph sync looks its teams back up through
/// <c>BaseTeams</c>, which is EF's TPH base set and so contains rows added through <c>Teams</c> and
/// <c>TeamOfTeams</c> — the in-memory fake keeps three unrelated lists, so the lookup finds nothing there
/// however correct the pass is. And the node write itself is raw SQL against a SQL Server graph table.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class TeamImportDefinitionTests
{
    private static readonly LocalDate ActiveDate = new(2026, 1, 1);

    private readonly SqlServerDbContextFixture _fixture;

    public TeamImportDefinitionTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static TeamImportDefinition CreateDefinition(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        dateTimeProvider.SetupGet(d => d.Today).Returns(SqlServerDbContextFixture.FixedNow.InUtc().Date);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        return new TeamImportDefinition(
            context, dateTimeProvider.Object, currentUser.Object, new ImportPayloadSerializer());
    }

    private static ImportProcessRow[] Rows(
        TeamImportDefinition definition, params (TeamType Type, string Name, string Code)[] teams) =>
        [.. teams.Select((t, i) => ImportProcessRow.Create(
            $"r{i + 1}", i + 1,
            definition.SerializeRow(new ImportTeamDto(t.Type, t.Name, new TeamCode(t.Code), null, ActiveDate))))];

    /// <summary>Runs the real runner over a real import process, so both passes run in order with the save between.</summary>
    private async Task<ImportProcess> RunImport(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context,
        TeamImportDefinition definition,
        ImportProcessRow[] rows,
        CancellationToken cancellationToken)
    {
        var process = ImportProcess.Create(
            TeamImportDefinition.ImportKey, "user-1", null, rows, SqlServerDbContextFixture.FixedNow);

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
    public async Task ARun_CreatesEachTeamAndMirrorsItIntoTheGraph()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition,
            (TeamType.Team, "Payments Core", "PAY"),
            (TeamType.TeamOfTeams, "Payments Group", "PAYG"));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert — the second pass finds its teams through the TPH base set, which only exists here
        process.Status.Should().Be(ImportProcessStatus.Succeeded);

        await using var assertContext = _fixture.CreateContext();
        var nodes = await assertContext.Set<TeamNode>().CountAsync(cancellationToken);
        nodes.Should().Be(2);

        var teams = await assertContext.BaseTeams.CountAsync(cancellationToken);
        teams.Should().Be(2);
    }

    [Fact]
    public async Task ARun_AppliesNothingWhenACodeIsAlreadyTaken()
    {
        // Arrange — the import is atomic, so one collision keeps the whole file out
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var seedContext = _fixture.CreateContext();
        var seedDefinition = CreateDefinition(seedContext);
        await RunImport(seedContext, seedDefinition, Rows(seedDefinition, (TeamType.Team, "Payments Core", "PAY")), cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = Rows(definition,
            (TeamType.Team, "Platform Enablement", "PLAT"),
            (TeamType.Team, "Payments Core Again", "PAY"));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert
        process.Status.Should().Be(ImportProcessStatus.Failed);

        await using var assertContext = _fixture.CreateContext();
        var codes = await assertContext.BaseTeams.Select(t => t.Code).ToListAsync(cancellationToken);
        codes.Select(c => c.Value).Should().BeEquivalentTo(["PAY"]);
    }
}
