using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="CreateStrategicInitiativeKpiCommandHandler"/> against a real SQL Server container.
/// <para>
/// A new KPI is ordered after the initiative's existing KPIs, so the handler must load them. Against the in-memory fake every KPI is always loaded, because <c>.Include</c> is a no-op, so
/// only a real query shows a handler that loads too few of them.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class CreateStrategicInitiativeKpiCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task CreateKpi_OrdersTheNewKpiAfterTheExistingOnes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var initiativeId = await SeedInitiativeWithKpis(2, ct);

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var handler = new CreateStrategicInitiativeKpiCommandHandler(
                context, Mock.Of<ILogger<CreateStrategicInitiativeKpiCommandHandler>>(), CurrentUser(), Clock());
            var result = await handler.Handle(new CreateStrategicInitiativeKpiCommand(initiativeId, Parameters("Third")), ct);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }

        // Assert
        (await Orders(initiativeId, ct)).Should().Equal(("KPI 1", 1), ("KPI 2", 2), ("Third", 3));
    }

    private async Task<Guid> SeedInitiativeWithKpis(int count, CancellationToken cancellationToken)
    {
        await _fixture.ResetPpmData(cancellationToken);
        await using var context = _fixture.CreateContext();
        var now = SqlServerDbContextFixture.FixedNow;

        var portfolio = ProjectPortfolio.Create("Delivery", "Delivery portfolio", null, EventActor.System, now);
        portfolio.Activate(PpmActor.System, now.InUtc().Date, now);
        var initiative = portfolio.CreateStrategicInitiative(
            "Atlas", "Move the estate", new LocalDateRange(now.InUtc().Date, now.InUtc().Date.PlusDays(90)), null, EventActor.System, now).Value;

        for (var i = 1; i <= count; i++)
        {
            initiative.CreateKpi(Parameters($"KPI {i}"), EventActor.System, now);
        }

        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return initiative.Id;
    }

    private async Task<List<(string Name, int Order)>> Orders(Guid initiativeId, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var kpis = await context.StrategicInitiatives
            .Where(i => i.Id == initiativeId)
            .SelectMany(i => i.Kpis)
            .OrderBy(k => k.Order)
            .Select(k => new { k.Name, k.Order })
            .ToListAsync(cancellationToken);

        return [.. kpis.Select(k => (k.Name, k.Order))];
    }

    private static StrategicInitiativeKpiUpsertParameters Parameters(string name) =>
        new(name, null, null, 100, null, "%", KpiTargetDirection.Increase);

    private static ICurrentUser CurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");
        return currentUser.Object;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        return clock.Object;
    }
}
