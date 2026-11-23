using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

/// <summary>
/// An active portfolio holding one active program and one project, seeded through EF exactly as production
/// writes it, for the rules that refuse to close a parent while a child is open.
/// </summary>
/// <param name="PortfolioId">The portfolio.</param>
/// <param name="ProgramId">The portfolio's one program.</param>
/// <param name="ProjectId">The portfolio's one project, in the program unless seeded standalone.</param>
/// <param name="EmployeeId">An employee to act as.</param>
public sealed record ActivePortfolioWithProgram(Guid PortfolioId, Guid ProgramId, Guid ProjectId, Guid EmployeeId)
{
    /// <summary>
    /// Resets the PPM data and seeds the portfolio. The project is left Proposed — open — unless
    /// <paramref name="cancelTheProject"/>; the program is left Active unless <paramref name="cancelTheProgram"/>.
    /// </summary>
    /// <param name="projectInProgram">
    /// False puts the project directly in the portfolio, so the program can be closed while the project stays open.
    /// </param>
    public static async Task<ActivePortfolioWithProgram> Seed(
        SqlServerDbContextFixture fixture,
        bool cancelTheProject,
        bool cancelTheProgram,
        CancellationToken cancellationToken,
        bool projectInProgram = true)
    {
        await fixture.ResetPpmData(cancellationToken);
        await using var context = fixture.CreateContext();

        var now = SqlServerDbContextFixture.FixedNow;
        var today = now.InUtc().Date;

        var employee = Employee.Create(
            new PersonName("Ada", null, "Lovelace"),
            "E1000",
            hireDate: now,
            new EmailAddress("ada.lovelace@acme.example"),
            jobTitle: "Delivery Lead",
            department: "Engineering",
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            now);
        await context.Employees.AddAsync(employee, cancellationToken);

        var category = ExpenditureCategory.Create("Capital", "Capital spend", isCapitalizable: true, requiresDepreciation: true);
        await context.ExpenditureCategories.AddAsync(category, cancellationToken);

        var portfolio = ProjectPortfolio.Create("Delivery", "Delivery portfolio", null, EventActor.System, now);
        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Started well before the system calendar's day, so closing on that day is never earlier than the start.
        Succeed(portfolio.Activate(PpmActor.System, today.PlusDays(-30), now));

        var program = Succeed(portfolio.CreateProgram(
            "Apollo",
            "Apollo program",
            new LocalDateRange(today.PlusDays(-30), today.PlusMonths(6)),
            roles: null,
            strategicThemes: null,
            EventActor.System,
            now));
        Succeed(program.Activate(PpmActor.System, ProgramAncestryRoles.None, now));

        var project = Succeed(portfolio.CreateProject(
            "Gemini",
            "Gemini description",
            new ProjectKey("GEMINI"),
            category.Id,
            dateRange: new LocalDateRange(today.PlusDays(-10), today.PlusMonths(3)),
            programId: projectInProgram ? program.Id : null,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: null,
            now,
            PpmActor.System));

        if (cancelTheProject)
        {
            Succeed(project.Cancel(PpmActor.System, ProjectAncestryRoles.None, now));
        }
        if (cancelTheProgram)
        {
            Succeed(program.Cancel(PpmActor.System, ProgramAncestryRoles.None, now));
        }

        await context.SaveChangesAsync(cancellationToken);

        return new ActivePortfolioWithProgram(portfolio.Id, program.Id, project.Id, employee.Id);
    }

    // A seed step that fails would otherwise leave a fixture other than the one the test describes.
    private static void Succeed(CSharpFunctionalExtensions.Result result)
    {
        if (result.IsFailure)
            throw new InvalidOperationException($"Seeding failed: {result.Error}");
    }

    private static T Succeed<T>(CSharpFunctionalExtensions.Result<T> result)
    {
        Succeed((CSharpFunctionalExtensions.Result)result);
        return result.Value;
    }
}
