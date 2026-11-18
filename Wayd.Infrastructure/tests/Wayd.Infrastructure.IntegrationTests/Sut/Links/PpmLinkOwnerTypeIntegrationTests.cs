using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.IntegrationTests.Sut.Notifications;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Application.Portfolios;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Links;

/// <summary>
/// Who may change a project's, program's and portfolio's links, decided against SQL Server.
/// </summary>
/// <remarks>
/// Leadership inherits from a project's program and portfolio, and a program's portfolio, whose roles each owner type
/// must load with <c>.Include</c>. The in-memory fakes ignore includes, so only a real provider catches one going
/// missing — which would silently refuse a program's or portfolio's leader.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class PpmLinkOwnerTypeIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 11, 17, 9, 0);

    private sealed record Seeded(Guid ProjectId, Guid ProgramId, Guid PortfolioId, Guid ProjectOwner, Guid ProjectSponsor, Guid ProgramManager, Guid PortfolioOwner);

    [Fact]
    public async Task ProjectLinks_ShouldBeManagedByEveryDeliveryLeader_WhereverTheirRoleIs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seeded = await Seed(ct);

        // Act
        var projectOwner = await Manages(seeded.ProjectOwner, (c, p, u) => new ProjectLinkOwnerType(c, p, u), seeded.ProjectId, ct);
        var programManager = await Manages(seeded.ProgramManager, (c, p, u) => new ProjectLinkOwnerType(c, p, u), seeded.ProjectId, ct);
        var portfolioOwner = await Manages(seeded.PortfolioOwner, (c, p, u) => new ProjectLinkOwnerType(c, p, u), seeded.ProjectId, ct);
        var sponsor = await Manages(seeded.ProjectSponsor, (c, p, u) => new ProjectLinkOwnerType(c, p, u), seeded.ProjectId, ct);

        // Assert
        projectOwner.Should().BeTrue();
        programManager.Should().BeTrue();
        portfolioOwner.Should().BeTrue();
        sponsor.Should().BeFalse();
    }

    [Fact]
    public async Task ProgramLinks_ShouldBeManagedByTheProgramsAndThePortfoliosLeaders_ButNotAProjects()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seeded = await Seed(ct);

        // Act
        var programManager = await Manages(seeded.ProgramManager, (c, p, u) => new ProgramLinkOwnerType(c, p, u), seeded.ProgramId, ct);
        var portfolioOwner = await Manages(seeded.PortfolioOwner, (c, p, u) => new ProgramLinkOwnerType(c, p, u), seeded.ProgramId, ct);
        var projectOwner = await Manages(seeded.ProjectOwner, (c, p, u) => new ProgramLinkOwnerType(c, p, u), seeded.ProgramId, ct);

        // Assert
        programManager.Should().BeTrue();
        portfolioOwner.Should().BeTrue();
        projectOwner.Should().BeFalse();
    }

    [Fact]
    public async Task PortfolioLinks_ShouldBeManagedByThePortfoliosLeadersOnly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seeded = await Seed(ct);

        // Act
        var portfolioOwner = await Manages(seeded.PortfolioOwner, (c, p, u) => new PortfolioLinkOwnerType(c, p, u), seeded.PortfolioId, ct);
        var programManager = await Manages(seeded.ProgramManager, (c, p, u) => new PortfolioLinkOwnerType(c, p, u), seeded.PortfolioId, ct);

        // Assert
        portfolioOwner.Should().BeTrue();
        programManager.Should().BeFalse();
    }

    /// <summary>Whether the employee, holding every permission but the PPM administrator's, may manage the record's links.</summary>
    private async Task<bool> Manages(
        Guid employeeId,
        Func<WaydDbContext, ICurrentPrincipal, ICurrentUser, ILinkOwnerType> ownerType,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) => !permission.EndsWith(".Administer", StringComparison.Ordinal));
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        var user = Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1");

        await using var context = fixture.CreateContext();
        var access = await ownerType(context, principal.Object, user).Resolve(recordId, cancellationToken);

        access.Should().NotBeNull();
        return access!.CanManage;
    }

    private async Task<Seeded> Seed(CancellationToken cancellationToken)
    {
        var projectOwner = await NotificationTestData.SeedEmployee(fixture);
        var projectSponsor = await NotificationTestData.SeedEmployee(fixture);
        var programManager = await NotificationTestData.SeedEmployee(fixture);
        var portfolioOwner = await NotificationTestData.SeedEmployee(fixture);

        var category = ExpenditureCategory.Create($"Cat {Guid.NewGuid():N}"[..12], "Link test category", false, false);
        category.Activate().IsSuccess.Should().BeTrue();

        var portfolio = ProjectPortfolio.Create($"Portfolio {Guid.NewGuid():N}"[..16], "Link test portfolio",
            new() { [ProjectPortfolioRole.Owner] = [portfolioOwner.Id] }, EventActor.System, Timestamp);
        portfolio.Activate(PpmActor.System, new LocalDate(2026, 1, 1), Timestamp).IsSuccess.Should().BeTrue();

        var program = portfolio.CreateProgram($"Program {Guid.NewGuid():N}"[..16], "Link test program",
            new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31)),
            new() { [ProgramRole.Manager] = [programManager.Id] }, null, EventActor.System, Timestamp).Value;
        program.Activate(PpmActor.System, ProgramAncestryRoles.None, Timestamp).IsSuccess.Should().BeTrue();

        await using var context = fixture.CreateContext();
        context.ExpenditureCategories.Add(category);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync(cancellationToken);

        var key = new ProjectKey($"L{Guid.NewGuid():N}"[..12].ToUpperInvariant());
        var project = portfolio.CreateProject("Linked project", "Link test project", key, category.Id, null,
            program.Id, null, null,
            new() { [ProjectRole.Owner] = [projectOwner.Id], [ProjectRole.Sponsor] = [projectSponsor.Id] },
            null, Timestamp, PpmActor.System);
        project.IsSuccess.Should().BeTrue(project.IsFailure ? project.Error : null);
        await context.SaveChangesAsync(cancellationToken);

        return new Seeded(project.Value.Id, program.Id, portfolio.Id, projectOwner.Id, projectSponsor.Id, programManager.Id, portfolioOwner.Id);
    }
}
