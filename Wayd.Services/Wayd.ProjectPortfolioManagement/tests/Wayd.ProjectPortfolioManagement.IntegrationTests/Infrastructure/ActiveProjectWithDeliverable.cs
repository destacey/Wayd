using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

/// <summary>
/// An active project with one deliverable, seeded through EF exactly as production writes it, on the seeded
/// default deliverable workflow.
/// </summary>
/// <param name="ProjectId">The project.</param>
/// <param name="EmployeeId">An employee to act as.</param>
/// <param name="DeliverableId">The project's one deliverable.</param>
/// <param name="Statuses">The default workflow's statuses, by meaning.</param>
public sealed record ActiveProjectWithDeliverable(
    Guid ProjectId,
    Guid EmployeeId,
    Guid DeliverableId,
    IReadOnlyDictionary<ProjectDeliverableStatusAlias, StatusRef> Statuses)
{
    /// <summary>A clock fixed at <see cref="SqlServerDbContextFixture.FixedNow"/>.</summary>
    public static IDateTimeProvider Clock()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        dateTimeProvider.SetupGet(d => d.Today).Returns(SqlServerDbContextFixture.FixedNow.InUtc().Date);
        return dateTimeProvider.Object;
    }

    /// <summary>Resets the PPM data and seeds the project, its deliverable accepted or still Not Started.</summary>
    /// <param name="employeeOwnsThePortfolio">
    /// Makes the employee the portfolio's owner, so they lead the project through its portfolio alone — they hold
    /// no role on the project itself.
    /// </param>
    public static async Task<ActiveProjectWithDeliverable> Seed(
        SqlServerDbContextFixture fixture,
        bool acceptTheDeliverable,
        CancellationToken cancellationToken,
        bool employeeOwnsThePortfolio = false)
    {
        await fixture.ResetPpmData(cancellationToken);
        await using var context = fixture.CreateContext();

        await new PpmWorkflowSeeder().Initialize(context, Clock(), cancellationToken);
        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .SingleAsync(w => w.OwnerType == PpmWorkflowOwners.ProjectDeliverable.Key && w.IsSystem, cancellationToken);
        var statuses = Enum.GetValues<ProjectDeliverableStatusAlias>()
            .Where(a => a != ProjectDeliverableStatusAlias.None)
            .ToDictionary(a => a, a => StatusRef.From(workflow.StatusFor((int)a)!));

        var employee = Employee.Create(
            new PersonName("Ada", null, "Lovelace"),
            "E1000",
            hireDate: SqlServerDbContextFixture.FixedNow,
            new EmailAddress("ada.lovelace@acme.example"),
            jobTitle: "Delivery Lead",
            department: "Engineering",
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            SqlServerDbContextFixture.FixedNow);
        await context.Employees.AddAsync(employee, cancellationToken);

        var category = ExpenditureCategory.Create("Capital", "Capital spend", isCapitalizable: true, requiresDepreciation: true);
        await context.ExpenditureCategories.AddAsync(category, cancellationToken);

        var portfolio = ProjectPortfolio.Create(
            "Delivery", "Delivery portfolio", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var today = SqlServerDbContextFixture.FixedNow.InUtc().Date;
        portfolio.Activate(PpmActor.System, today, SqlServerDbContextFixture.FixedNow);
        if (employeeOwnsThePortfolio)
        {
            portfolio.UpdateRoles(PpmActor.System, new() { [ProjectPortfolioRole.Owner] = [employee.Id] }, SqlServerDbContextFixture.FixedNow);
        }
        var project = portfolio.CreateProject(
            "Gemini",
            "Gemini description",
            new ProjectKey("GEMINI"),
            category.Id,
            dateRange: new LocalDateRange(today.PlusDays(-10), today.PlusMonths(3)),
            programId: null,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: null,
            SqlServerDbContextFixture.FixedNow,
            PpmActor.System).Value;
        project.Activate(PpmActor.System, ProjectAncestryRoles.None, SqlServerDbContextFixture.FixedNow);

        var deliverable = project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, "Customer portal", null, null, null, null,
            statuses[ProjectDeliverableStatusAlias.NotStarted], SqlServerDbContextFixture.FixedNow).Value;
        if (acceptTheDeliverable)
        {
            project.ChangeDeliverableStatus(PpmActor.System, ProjectAncestryRoles.None, deliverable.Id,
                statuses[ProjectDeliverableStatusAlias.Accepted], null, SqlServerDbContextFixture.FixedNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new ActiveProjectWithDeliverable(project.Id, employee.Id, deliverable.Id, statuses);
    }
}
