using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler reads each project's and program's theme tags through a
/// collection projection; the in-memory fake evaluates that as LINQ to Objects.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetProjectClassificationsQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ResolvesEachProjectsPortfolioProgramAndEffectiveThemes()
    {
        // Arrange — INHERIT is in a program tagged Growth and has no themes; OWN has its own Trust theme
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetPpmData(cancellationToken);
        var now = SqlServerDbContextFixture.FixedNow;
        Guid portfolioId, programId, inheritId, ownId, growthId, trustId;

        await using (var context = _fixture.CreateContext())
        {
            var growth = new StrategicTheme(new SourceTheme(Guid.NewGuid(), Random.Shared.Next(100_000, 999_999), "Growth"), now);
            var trust = new StrategicTheme(new SourceTheme(Guid.NewGuid(), Random.Shared.Next(100_000, 999_999), "Trust"), now);
            await context.PpmStrategicThemes.AddRangeAsync([growth, trust], cancellationToken);

            var category = ExpenditureCategory.Create("Operating", "Operating spend", isCapitalizable: false, requiresDepreciation: false);
            await context.ExpenditureCategories.AddAsync(category, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var portfolio = ProjectPortfolio.Create("Delivery", "Delivery portfolio", null, EventActor.System, now);
            await context.Portfolios.AddAsync(portfolio, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            portfolio.Activate(PpmActor.System, now.InUtc().Date, now);

            var today = now.InUtc().Date;
            var program = portfolio.CreateProgram(
                "Onboarding", "Onboarding program", new LocalDateRange(today, today.PlusMonths(6)), null, [growth.Id], EventActor.System, now).Value;
            program.Activate(PpmActor.System, ProgramAncestryRoles.None, now).IsSuccess.Should().BeTrue();
            var inherit = portfolio.CreateProject(
                "Inherit", "Inherits its themes", new ProjectKey("INHERIT"), category.Id, null, program.Id,
                null, null, null, null, now, PpmActor.System).Value;
            var own = portfolio.CreateProject(
                "Own", "Has its own themes", new ProjectKey("OWN"), category.Id, null, null,
                null, null, null, [trust.Id], now, PpmActor.System).Value;
            await context.SaveChangesAsync(cancellationToken);

            (portfolioId, programId, inheritId, ownId, growthId, trustId) = (portfolio.Id, program.Id, inherit.Id, own.Id, growth.Id, trust.Id);
        }

        await using var queryContext = _fixture.CreateContext();
        var handler = new GetProjectClassificationsQueryHandler(queryContext);

        // Act
        var result = await handler.Handle(new GetProjectClassificationsQuery([inheritId, ownId]), cancellationToken);

        // Assert
        var inherited = result.Single(p => p.ProjectId == inheritId);
        inherited.Portfolio.Id.Should().Be(portfolioId);
        inherited.Program!.Id.Should().Be(programId);
        inherited.Themes.Select(t => t.Id).Should().Equal(growthId);
        inherited.ThemesFromProgram.Should().BeTrue();

        var owned = result.Single(p => p.ProjectId == ownId);
        owned.ProjectKey.Should().Be("OWN");
        owned.Program.Should().BeNull();
        owned.Themes.Select(t => t.Id).Should().Equal(trustId);
        owned.ThemesFromProgram.Should().BeFalse();
    }

    private sealed record SourceTheme(Guid Id, int Key, string Name) : IStrategicThemeData
    {
        public string Description => $"{Name} theme";
        public StrategicThemeState State => StrategicThemeState.Active;
    }
}
