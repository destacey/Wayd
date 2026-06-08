using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectPortfolioFaker : PrivateConstructorFaker<ProjectPortfolio>
{
    public ProjectPortfolioFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => f.Commerce.Department());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.Status, f => ProjectPortfolioStatus.Proposed);
        RuleFor(x => x.DateRange, f => null); // Default is null, as proposed portfolios may not have dates.
    }
}

public static class ProjectPortfolioFakerExtensions
{
    public static ProjectPortfolioFaker WithData(
        this ProjectPortfolioFaker faker,
        Guid? id = null,
        int? key = null,
        string? name = null,
        string? description = null,
        ProjectPortfolioStatus? status = null,
        Dictionary<ProjectPortfolioRole, HashSet<Guid>>? roles = null,
        FlexibleDateRange? dateRange = null)
    {
        if (id.HasValue) { faker.RuleFor(x => x.Id, id.Value); }
        if (key.HasValue) { faker.RuleFor(x => x.Key, key.Value); }
        if (!string.IsNullOrWhiteSpace(name)) { faker.RuleFor(x => x.Name, name); }
        if (!string.IsNullOrWhiteSpace(description)) { faker.RuleFor(x => x.Description, description); }
        if (status.HasValue) { faker.RuleFor(x => x.Status, status); }

        if (roles is not null)
        {
            var portfolioId = id ?? Guid.NewGuid();
            if (!id.HasValue)
            {
                faker.RuleFor(x => x.Id, portfolioId);
            }

            HashSet<RoleAssignment<ProjectPortfolioRole>> updatedRoles = [];
            foreach (var role in roles)
            {
                foreach (var employeeId in role.Value)
                {
                    updatedRoles.Add(new RoleAssignment<ProjectPortfolioRole>(portfolioId, role.Key, employeeId));
                }
            }

            faker.RuleFor("_roles", x => updatedRoles);
        }

        if (dateRange is not null) { faker.RuleFor(x => x.DateRange, dateRange); }

        return faker;
    }

    /// <summary>
    /// Generates a proposed portfolio.
    /// </summary>
    public static ProjectPortfolio AsProposed(this ProjectPortfolioFaker faker)
    {
        return faker.WithData(status: ProjectPortfolioStatus.Proposed).Generate();
    }

    /// <summary>
    /// Generates an active portfolio with a start date 10 days ago.
    /// </summary>
    public static ProjectPortfolio AsActive(this ProjectPortfolioFaker faker, TestingDateTimeProvider dateTimeProvider)
    {
        var now = dateTimeProvider.Today;
        var defaultStartDate = now.PlusDays(-10);

        return faker.WithData(
            status: ProjectPortfolioStatus.Active,
            dateRange: new FlexibleDateRange(defaultStartDate)
        ).Generate();
    }

    /// <summary>
    /// Generates a closed portfolio with a start date 20 days ago and end date 10 days ago.
    /// </summary>
    public static ProjectPortfolio AsClosed(this ProjectPortfolioFaker faker, TestingDateTimeProvider dateTimeProvider)
    {
        var now = dateTimeProvider.Today;
        var defaultStartDate = now.PlusDays(-20);
        var defaultEndDate = now.PlusDays(-10);

        return faker.WithData(
            status: ProjectPortfolioStatus.Closed,
            dateRange: new FlexibleDateRange(defaultStartDate, defaultEndDate)
        ).Generate();
    }

    /// <summary>
    /// Generates an archived portfolio with a start date 20 days ago and end date 10 days ago.
    /// </summary>
    public static ProjectPortfolio AsArchived(this ProjectPortfolioFaker faker, TestingDateTimeProvider dateTimeProvider)
    {
        var now = dateTimeProvider.Today;
        var defaultStartDate = now.PlusDays(-20);
        var defaultEndDate = now.PlusDays(-10);

        return faker.WithData(
            status: ProjectPortfolioStatus.Archived,
            dateRange: new FlexibleDateRange(defaultStartDate, defaultEndDate)
        ).Generate();
    }

    /// <summary>
    /// Attaches the given projects to the portfolio's internal _projects set, simulating EF Core's
    /// Include behaviour for unit tests (the aggregate's collection is otherwise only populated by EF).
    /// Each project's PortfolioId is set to this portfolio's Id so the graph is internally consistent.
    /// </summary>
    public static ProjectPortfolio WithProjects(this ProjectPortfolio portfolio, params Project[] projects)
    {
        foreach (var project in projects)
        {
            project.SetPrivate(p => p.PortfolioId, portfolio.Id);
        }

        portfolio.SetPrivateField("_projects", new HashSet<Project>(projects));
        return portfolio;
    }
}
