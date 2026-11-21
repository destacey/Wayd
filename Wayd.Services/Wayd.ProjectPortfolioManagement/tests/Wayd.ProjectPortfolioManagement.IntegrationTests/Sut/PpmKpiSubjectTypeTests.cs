using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Portfolios;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Who may manage a project's, a program's and a portfolio's KPIs, against a real SQL Server container.
/// </summary>
/// <remarks>
/// Leadership inherits down from the portfolio and the program, read through navigations the subject types must
/// include. An in-memory fake holds the navigations whether or not they are included, so only a real provider shows
/// a missing <c>.Include</c> — which would silently refuse every inherited leader.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class PpmKpiSubjectTypeTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private sealed record Seeded(
        Guid PortfolioId, Guid ProgramId, Guid ProjectId,
        Guid PortfolioOwnerId, Guid ProgramManagerId, Guid PortfolioSponsorId, Guid OutsiderId);

    [Fact]
    public async Task Resolve_LetsLeadersOfTheRecordOrAnAncestorManage_AndNoOneElse()
    {
        // Arrange — GEMINI sits in a program in a portfolio, with no roles of its own
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await using var context = _fixture.CreateContext();

        // Act
        var project = new Dictionary<Guid, bool>();
        var program = new Dictionary<Guid, bool>();
        var portfolio = new Dictionary<Guid, bool>();
        foreach (var employeeId in new[] { seeded.PortfolioOwnerId, seeded.ProgramManagerId, seeded.PortfolioSponsorId, seeded.OutsiderId })
        {
            var (principal, user) = Caller(employeeId);
            project[employeeId] = (await new ProjectKpiSubjectType(context, principal, user).Resolve(seeded.ProjectId, cancellationToken))!.CanManage;
            program[employeeId] = (await new ProgramKpiSubjectType(context, principal, user).Resolve(seeded.ProgramId, cancellationToken))!.CanManage;
            portfolio[employeeId] = (await new PortfolioKpiSubjectType(context, principal, user).Resolve(seeded.PortfolioId, cancellationToken))!.CanManage;
        }

        // Assert
        project[seeded.PortfolioOwnerId].Should().BeTrue();
        project[seeded.ProgramManagerId].Should().BeTrue();
        project[seeded.PortfolioSponsorId].Should().BeFalse();
        project[seeded.OutsiderId].Should().BeFalse();

        program[seeded.PortfolioOwnerId].Should().BeTrue();
        program[seeded.ProgramManagerId].Should().BeTrue();
        program[seeded.PortfolioSponsorId].Should().BeFalse();
        program[seeded.OutsiderId].Should().BeFalse();

        portfolio[seeded.PortfolioOwnerId].Should().BeTrue();
        portfolio[seeded.ProgramManagerId].Should().BeFalse();
        portfolio[seeded.PortfolioSponsorId].Should().BeFalse();
        portfolio[seeded.OutsiderId].Should().BeFalse();
    }

    /// <summary>A caller holding every View and Update PPM permission but not the PPM administrator's.</summary>
    private static (ICurrentPrincipal, ICurrentUser) Caller(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HoldsPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.GetUserId()).Returns($"user-{employeeId}");

        return (principal.Object, user.Object);
    }

    private async Task<Seeded> Seed(CancellationToken cancellationToken)
    {
        await _fixture.ResetPpmData(cancellationToken);
        await using var context = _fixture.CreateContext();
        var now = SqlServerDbContextFixture.FixedNow;

        Employee NewEmployee(string number, string first) => Employee.Create(
            new PersonName(first, null, "Tester"),
            number,
            hireDate: now,
            new EmailAddress($"{first.ToLowerInvariant()}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            now);

        var owner = NewEmployee("E3001", "Ada");
        var manager = NewEmployee("E3002", "Grace");
        var sponsor = NewEmployee("E3003", "Alan");
        var outsider = NewEmployee("E3004", "Edsger");
        await context.Employees.AddRangeAsync([owner, manager, sponsor, outsider], cancellationToken);

        var category = ExpenditureCategory.Create("Capital", "Capital spend", isCapitalizable: true, requiresDepreciation: true);
        await context.ExpenditureCategories.AddAsync(category, cancellationToken);

        var portfolio = ProjectPortfolio.Create("Delivery", "Delivery portfolio", null, EventActor.System, now);
        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var today = now.InUtc().Date;
        portfolio.Activate(PpmActor.System, today, now).IsSuccess.Should().BeTrue();
        portfolio.UpdateRoles(PpmActor.System, new()
        {
            [ProjectPortfolioRole.Owner] = [owner.Id],
            [ProjectPortfolioRole.Sponsor] = [sponsor.Id],
        }, now).IsSuccess.Should().BeTrue();

        var program = portfolio.CreateProgram(
            "Apollo", "Apollo program", new LocalDateRange(today, today.PlusMonths(6)), new() { [ProgramRole.Manager] = [manager.Id] }, null, EventActor.System, now).Value;
        program.Activate(PpmActor.System, ProgramAncestryRoles.None, now).IsSuccess.Should().BeTrue();

        var project = portfolio.CreateProject(
            "Gemini",
            "Gemini description",
            new ProjectKey("GEMINI"),
            category.Id,
            dateRange: null,
            programId: program.Id,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: null,
            now,
            PpmActor.System).Value;

        await context.SaveChangesAsync(cancellationToken);

        return new Seeded(portfolio.Id, program.Id, project.Id, owner.Id, manager.Id, sponsor.Id, outsider.Id);
    }
}
