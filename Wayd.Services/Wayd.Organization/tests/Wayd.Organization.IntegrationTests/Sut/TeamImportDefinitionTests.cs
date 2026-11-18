using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Links;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Domain;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The team import against a real SQL Server container.
/// </summary>
/// <remarks>
/// What only a real provider shows: the import looks teams up through <c>BaseTeams</c>, which is EF's TPH
/// base set and so contains rows added through <c>Teams</c> and <c>TeamOfTeams</c> — the in-memory fake
/// keeps three unrelated lists, so a lookup there finds nothing however correct the pass is.
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

        OrganizationLinkOwners.Register();

        return new TeamImportDefinition(
            context, dateTimeProvider.Object, currentUser.Object,
            Mock.Of<ISettings<SchedulingSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new SchedulingSettings())),
            Mock.Of<ISettings<GeneralSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new GeneralSettings())),
            new LinkWriter(context, [], dateTimeProvider.Object),
            new ImportPayloadSerializer());
    }

    private static ImportProcessRow[] Rows(
        TeamImportDefinition definition, params (TeamType Type, string Name, string Code)[] teams) =>
        [.. teams.Select((t, i) => ImportProcessRow.Create(
            $"r{i + 1}", i + 1,
            definition.SerializeRow(new ImportTeamDto(t.Type, t.Name, new TeamCode(t.Code), null, ActiveDate))))];

    private static ImportProcessRow[] RowsWithLinks(
        TeamImportDefinition definition, params (string Name, string Code, string Links)[] teams) =>
        [.. teams.Select((t, i) => ImportProcessRow.Create(
            $"r{i + 1}", i + 1,
            definition.SerializeRow(new ImportTeamDto(TeamType.Team, t.Name, new TeamCode(t.Code), null, ActiveDate, Links: t.Links))))];

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
    public async Task ARun_CreatesEachTeam()
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

        // Assert
        process.Status.Should().Be(ImportProcessStatus.Succeeded);

        await using var assertContext = _fixture.CreateContext();
        var teams = await assertContext.BaseTeams.CountAsync(cancellationToken);
        teams.Should().Be(2);
    }

    [Fact]
    public async Task ARun_CommitsEachTeamWithItsLinks()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = RowsWithLinks(definition,
            ("Payments Core", "PAY", "Charter|https://example.com/charter; Wiki|https://example.com/wiki"),
            ("Platform Enablement", "PLAT", ""));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert
        process.Status.Should().Be(ImportProcessStatus.Succeeded);

        await using var assertContext = _fixture.CreateContext();
        var payId = await assertContext.BaseTeams.Where(t => t.Code == new TeamCode("PAY")).Select(t => t.Id).SingleAsync(cancellationToken);
        var platId = await assertContext.BaseTeams.Where(t => t.Code == new TeamCode("PLAT")).Select(t => t.Id).SingleAsync(cancellationToken);

        var links = await assertContext.Links
            .Where(l => l.OwnerId == payId || l.OwnerId == platId)
            .Select(l => new { l.OwnerType, l.OwnerId, l.Name, l.Url })
            .ToListAsync(cancellationToken);
        links.Should().BeEquivalentTo([
            new { OwnerType = "organization.team", OwnerId = payId, Name = "Charter", Url = "https://example.com/charter" },
            new { OwnerType = "organization.team", OwnerId = payId, Name = "Wiki", Url = "https://example.com/wiki" },
        ]);
    }

    [Fact]
    public async Task ARun_CommitsNoTeamAndNoLink_WhenALinkEntryIsMalformed()
    {
        // Arrange — the import is atomic, so the bad row keeps the good row and its links out too
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var definition = CreateDefinition(context);
        var rows = RowsWithLinks(definition,
            ("Payments Core", "PAY", "Charter|https://example.com/pay-charter"),
            ("Platform Enablement", "PLAT", "Wiki|ftp://example.com/wiki"));

        // Act
        var process = await RunImport(context, definition, rows, cancellationToken);

        // Assert
        process.Status.Should().Be(ImportProcessStatus.Failed);

        await using var assertContext = _fixture.CreateContext();
        (await assertContext.BaseTeams.CountAsync(cancellationToken)).Should().Be(0);
        (await assertContext.Links.AnyAsync(l => l.Url == "https://example.com/pay-charter", cancellationToken)).Should().BeFalse();
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
