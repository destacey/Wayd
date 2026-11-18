using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Organization.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Gate-Links-By-Owner migration's data steps against real SQL Server: each link is named for the record
/// it was on, a link whose record is gone is removed, and an address saved without a scheme is completed.
/// </summary>
/// <remarks>
/// The steps are hand-written T-SQL over other modules' tables, so only a real database shows they find a record
/// where it is stored. They run here against the current schema, on rows inserted as the old code left them: with no
/// owner type.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class GateLinksByOwnerMigrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 17, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task NameOwnerTypes_NamesALinkForTheRecordItIsOn_AndRemoveOrphans_RemovesTheRest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var portfolio = await SeedPortfolio(ct);
        var team = await SeedTeam(ct);
        var onPortfolio = await InsertUnnamedLink(portfolio.Id, "https://example.com/portfolio", ct);
        var onTeam = await InsertUnnamedLink(team.Id, "https://example.com/team", ct);
        var onNothing = await InsertUnnamedLink(Guid.CreateVersion7(), "https://example.com/orphan", ct);

        // Act
        await Run(GateLinksByOwner.NameOwnerTypesSql, ct);
        await Run(GateLinksByOwner.RemoveOrphansSql, ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var links = await verify.Links.AsNoTracking()
            .Where(l => l.Id == onPortfolio || l.Id == onTeam || l.Id == onNothing)
            .ToDictionaryAsync(l => l.Id, l => l.OwnerType, ct);

        links.Should().HaveCount(2);
        links[onPortfolio].Should().Be("ppm.portfolio");
        links[onTeam].Should().Be("organization.team");
    }

    [Fact]
    public async Task RemoveOrphans_RemovesTheLinksOfASoftDeletedRecord()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var team = await SeedTeam(ct);
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlAsync($"UPDATE [Organization].[Teams] SET [IsDeleted] = 1 WHERE [Id] = {team.Id}", ct);
        }

        var link = await InsertUnnamedLink(team.Id, "https://example.com/team", ct);

        // Act
        await Run(GateLinksByOwner.NameOwnerTypesSql, ct);
        await Run(GateLinksByOwner.RemoveOrphansSql, ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.Links.AnyAsync(l => l.Id == link, ct)).Should().BeFalse();
    }

    [Theory]
    [InlineData("example.com/charter", "https://example.com/charter")]
    [InlineData("  www.example.com", "https://www.example.com")]
    [InlineData("http://example.com/charter", "http://example.com/charter")]
    [InlineData("https://example.com/charter", "https://example.com/charter")]
    [InlineData("ftp://example.com/file", "ftp://example.com/file")]
    public async Task NormalizeUrls_CompletesOnlyAnAddressThePageOpenedAsHttps(string stored, string expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var link = await InsertUnnamedLink(Guid.CreateVersion7(), stored, ct);

        // Act
        await Run(GateLinksByOwner.NormalizeUrlsSql, ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.Links.AsNoTracking().Where(l => l.Id == link).Select(l => l.Url).SingleAsync(ct)).Should().Be(expected);
    }

    private async Task<ProjectPortfolio> SeedPortfolio(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var portfolio = ProjectPortfolio.Create($"Portfolio {Guid.NewGuid():N}"[..16], "Link migration test portfolio", [], EventActor.System, Now);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync(ct);
        return portfolio;
    }

    private async Task<Team> SeedTeam(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var code = new TeamCode($"L{Random.Shared.Next(10_000, 99_999)}");
        var team = Team.Create($"Links {code.Value} {Guid.NewGuid():N}", code, null, new LocalDate(2026, 1, 1), Methodology.Scrum, SizingMethod.StoryPoints,
            "America/Chicago", 1, WorkingWeek.MondayToFriday, EventActor.System, Now);
        context.Teams.Add(team);
        await context.SaveChangesAsync(ct);
        return team;
    }

    /// <summary>A link as the code before the migration stored it: an owner id and no owner type.</summary>
    private async Task<Guid> InsertUnnamedLink(Guid ownerId, string url, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO [Links].[Links] ([Id], [OwnerId], [OwnerType], [Name], [Url]) VALUES ({id}, {ownerId}, '', N'Charter', {url})", ct);
        return id;
    }

    private async Task Run(string sql, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        DbConnection connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
