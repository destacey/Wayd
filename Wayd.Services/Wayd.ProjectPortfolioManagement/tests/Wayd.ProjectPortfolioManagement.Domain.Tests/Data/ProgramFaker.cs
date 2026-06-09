using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProgramFaker : PrivateConstructorFaker<Program>
{
    public ProgramFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Commerce.Department());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.Status, f => ProgramStatus.Proposed);
        RuleFor(x => x.DateRange, f => null); // Default is null, as proposed programs may not have dates.
        RuleFor(x => x.PortfolioId, f => f.Random.Guid()); // Set by portfolio in real scenarios.
    }
}

public static class ProgramFakerExtensions
{
    public static ProgramFaker WithId(this ProgramFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProgramFaker WithName(this ProgramFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ProgramFaker WithDescription(this ProgramFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static ProgramFaker WithStatus(this ProgramFaker faker, ProgramStatus status)
    {
        faker.RuleFor(x => x.Status, status);

        return faker;
    }

    public static ProgramFaker WithDateRange(this ProgramFaker faker, LocalDateRange? dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);

        return faker;
    }

    public static ProgramFaker WithPortfolioId(this ProgramFaker faker, Guid portfolioId)
    {
        faker.RuleFor(x => x.PortfolioId, portfolioId);

        return faker;
    }

    public static ProgramFaker WithRoles(this ProgramFaker faker, Dictionary<ProgramRole, HashSet<Guid>>? roles)
    {
        faker.RuleFor("_roles", (_, program) =>
        {
            if (roles is null)
            {
                return new HashSet<RoleAssignment<ProgramRole>>();
            }

            HashSet<RoleAssignment<ProgramRole>> updatedRoles = [];
            foreach (var role in roles)
            {
                foreach (var employeeId in role.Value)
                {
                    updatedRoles.Add(new RoleAssignment<ProgramRole>(program.Id, role.Key, employeeId));
                }
            }

            return updatedRoles;
        });

        return faker;
    }

    /// <summary>
    /// Generates a proposed program without a date range.
    /// </summary>
    public static Program AsProposed(this ProgramFaker faker, Guid? portfolioId = null)
    {
        return faker
            .WithStatus(ProgramStatus.Proposed)
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    /// <summary>
    /// Generates an active program with a start date 10 days ago.
    /// </summary>
    public static Program AsActive(this ProgramFaker faker, TestingDateTimeProvider dateTimeProvider, Guid? portfolioId = null)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(-10);
        var endDate = startDate.PlusDays(10);

        return faker
            .WithStatus(ProgramStatus.Active)
            .WithDateRange(new LocalDateRange(startDate, endDate))
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    /// <summary>
    /// Generates a completed program with a start date 20 days ago and end date 10 days ago.
    /// </summary>
    public static Program AsCompleted(this ProgramFaker faker, TestingDateTimeProvider dateTimeProvider, Guid? portfolioId = null)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(-20);
        var endDate = startDate.PlusDays(5);

        return faker
            .WithStatus(ProgramStatus.Completed)
            .WithDateRange(new LocalDateRange(startDate, endDate))
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    /// <summary>
    /// Generates a cancelled program with a start date 15 days ago and an end date 5 days ago.
    /// </summary>
    public static Program AsCancelled(this ProgramFaker faker, TestingDateTimeProvider dateTimeProvider, Guid? portfolioId = null)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(-15);
        var endDate = startDate.PlusDays(5);

        return faker
            .WithStatus(ProgramStatus.Cancelled)
            .WithDateRange(new LocalDateRange(startDate, endDate))
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    private static ProgramFaker WithOptionalPortfolioId(this ProgramFaker faker, Guid? portfolioId)
    {
        return portfolioId.HasValue ? faker.WithPortfolioId(portfolioId.Value) : faker;
    }
}