using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.IntegrationTests.Sut.Notifications;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Discussions;

/// <summary>
/// Who moderates a project's conversation, decided against SQL Server.
/// </summary>
/// <remarks>
/// Leadership inherits from the project's program and portfolio, whose roles the channel type must load with
/// <c>.Include</c>. The in-memory fakes ignore includes, so only a real provider catches one going missing — which
/// would silently refuse a program's or portfolio's leader.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ProjectDiscussionChannelTypeIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 29, 9, 0);

    [Fact]
    public async Task Resolve_ShouldLetEveryDeliveryLeaderModerate_WhereverTheirRoleIs()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var projectOwner = await NotificationTestData.SeedEmployee(fixture);
        var programManager = await NotificationTestData.SeedEmployee(fixture);
        var portfolioOwner = await NotificationTestData.SeedEmployee(fixture);
        var projectSponsor = await NotificationTestData.SeedEmployee(fixture);
        var projectId = await SeedProject(projectOwner.Id, projectSponsor.Id, programManager.Id, portfolioOwner.Id, cancellationToken);

        // Act
        var projectOwnerModerates = await Moderates(projectOwner.Id, projectId, cancellationToken);
        var programManagerModerates = await Moderates(programManager.Id, projectId, cancellationToken);
        var portfolioOwnerModerates = await Moderates(portfolioOwner.Id, projectId, cancellationToken);
        var sponsorModerates = await Moderates(projectSponsor.Id, projectId, cancellationToken);

        // Assert
        projectOwnerModerates.Should().BeTrue();
        programManagerModerates.Should().BeTrue();
        portfolioOwnerModerates.Should().BeTrue();
        sponsorModerates.Should().BeFalse();
    }

    private async Task<bool> Moderates(Guid employeeId, Guid projectId, CancellationToken cancellationToken)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(ProjectReadRule.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

        await using var context = fixture.CreateContext();
        var access = await new ProjectDiscussionChannelType(context, principal.Object).Resolve(projectId, cancellationToken);

        access.Should().NotBeNull();
        access!.CanPost.Should().BeTrue();
        return access.CanModerate;
    }

    private async Task<Guid> SeedProject(Guid ownerId, Guid sponsorId, Guid programManagerId, Guid portfolioOwnerId,
        CancellationToken cancellationToken)
    {
        var category = ExpenditureCategory.Create($"Cat {Guid.NewGuid():N}"[..12], "Discussion test category", false, false);
        category.Activate().IsSuccess.Should().BeTrue();

        var portfolio = ProjectPortfolio.Create($"Portfolio {Guid.NewGuid():N}"[..16], "Discussion test portfolio",
            new() { [ProjectPortfolioRole.Owner] = [portfolioOwnerId] }, EventActor.System, Timestamp);
        portfolio.Activate(PpmActor.System, new LocalDate(2026, 1, 1), Timestamp).IsSuccess.Should().BeTrue();

        var program = portfolio.CreateProgram($"Program {Guid.NewGuid():N}"[..16], "Discussion test program",
            new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31)),
            new() { [ProgramRole.Manager] = [programManagerId] }, null, EventActor.System, Timestamp).Value;
        program.Activate(PpmActor.System, ProgramAncestryRoles.None, Timestamp).IsSuccess.Should().BeTrue();

        await using var context = fixture.CreateContext();
        context.ExpenditureCategories.Add(category);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync(cancellationToken);

        var key = new ProjectKey($"D{Guid.NewGuid():N}"[..12].ToUpperInvariant());
        var project = portfolio.CreateProject("Discussed project", "Discussion test project", key, category.Id, null,
            program.Id, null, null,
            new() { [ProjectRole.Owner] = [ownerId], [ProjectRole.Sponsor] = [sponsorId] },
            null, Timestamp, PpmActor.System);
        project.IsSuccess.Should().BeTrue(project.IsFailure ? project.Error : null);
        await context.SaveChangesAsync(cancellationToken);

        return project.Value.Id;
    }
}
