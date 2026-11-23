using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="DeleteStrategicInitiativeCommandHandler"/> against a real SQL Server container.
/// <para>
/// The initiative's project links reference it with no database cascade, so the delete must remove them itself.
/// The in-memory fake enforces no foreign keys, so only a real database shows a link left behind.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeleteStrategicInitiativeCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_WithALinkedProject_DeletesTheInitiativeAndItsLinksButNotTheProject()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActivePortfolioWithProgram.Seed(_fixture, cancelTheProject: false, cancelTheProgram: false, ct);
        var initiativeId = await SeedInitiativeLinkedTo(seed, ct);

        // Act
        var result = await Delete(initiativeId, ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.StrategicInitiatives.AnyAsync(i => i.Id == initiativeId, ct)).Should().BeFalse();
        (await verify.Projects
            .Where(p => p.Id == seed.ProjectId)
            .SelectMany(p => p.StrategicInitiativeProjects)
            .AnyAsync(ct)).Should().BeFalse();
        (await verify.Projects.AnyAsync(p => p.Id == seed.ProjectId, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ProjectLinkedToTwoInitiatives_RemovesOnlyTheDeletedInitiativesLink()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await ActivePortfolioWithProgram.Seed(_fixture, cancelTheProject: false, cancelTheProgram: false, ct);
        var deletedId = await SeedInitiativeLinkedTo(seed, ct, name: "Atlas", approve: true);
        var keptId = await SeedInitiativeLinkedTo(seed, ct, name: "Borealis");

        // Act
        var result = await Delete(deletedId, ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        var remaining = await verify.Projects
            .Where(p => p.Id == seed.ProjectId)
            .SelectMany(p => p.StrategicInitiativeProjects)
            .Select(l => l.StrategicInitiativeId)
            .ToListAsync(ct);
        remaining.Should().Equal(keptId);
    }

    private async Task<Guid> SeedInitiativeLinkedTo(
        ActivePortfolioWithProgram seed,
        CancellationToken ct,
        string name = "Atlas",
        bool approve = false)
    {
        await using var context = _fixture.CreateContext();
        var now = SqlServerDbContextFixture.FixedNow;
        var today = now.InUtc().Date;

        var portfolio = await context.Portfolios
            .Include(p => p.StrategicInitiatives)
            .SingleAsync(p => p.Id == seed.PortfolioId, ct);

        var initiative = portfolio.CreateStrategicInitiative(
            name,
            "Move the estate",
            new LocalDateRange(today, today.PlusDays(90)),
            null,
            EventActor.System,
            now).Value;
        if (approve)
        {
            initiative.Approve(EventActor.System, now).IsSuccess.Should().BeTrue();
        }
        initiative.ManageProjects([seed.ProjectId], EventActor.System, now).IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync(ct);
        return initiative.Id;
    }

    private async Task<CSharpFunctionalExtensions.Result> Delete(Guid initiativeId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await Handler(context).Handle(new DeleteStrategicInitiativeCommand(initiativeId), ct);
    }

    private static DeleteStrategicInitiativeCommandHandler Handler(WaydDbContext context) =>
        new(
            context,
            PpmAdministrator.User(),
            Mock.Of<ILogger<DeleteStrategicInitiativeCommandHandler>>(),
            ActiveProjectWithDeliverable.Clock(),
            Mock.Of<ILinkWriter>(),
            Mock.Of<IKpiWriter>());
}
