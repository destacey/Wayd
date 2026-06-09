using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public class StrategicInitiativeFaker : PrivateConstructorFaker<StrategicInitiative>
{
    public StrategicInitiativeFaker(TestingDateTimeProvider dateTimeProvider)
    {
        var start = dateTimeProvider.Now.Plus(Duration.FromDays(FakerHub.Random.Int(1, 20))).InUtc().LocalDateTime.Date;
        var end = start.PlusDays(FakerHub.Random.Int(1, 20));

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.Status, f => StrategicInitiativeStatus.Proposed);
        RuleFor(x => x.DateRange, f => new LocalDateRange(start, end));
        RuleFor(x => x.PortfolioId, f => f.Random.Guid());
    }
}

public static class StrategicInitiativeFakerExtensions
{
    public static StrategicInitiativeFaker WithId(this StrategicInitiativeFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static StrategicInitiativeFaker WithKey(this StrategicInitiativeFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static StrategicInitiativeFaker WithName(this StrategicInitiativeFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static StrategicInitiativeFaker WithDescription(this StrategicInitiativeFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static StrategicInitiativeFaker WithStatus(this StrategicInitiativeFaker faker, StrategicInitiativeStatus status)
    {
        faker.RuleFor(x => x.Status, status);

        return faker;
    }

    public static StrategicInitiativeFaker WithDateRange(this StrategicInitiativeFaker faker, LocalDateRange dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);

        return faker;
    }

    public static StrategicInitiativeFaker WithPortfolioId(this StrategicInitiativeFaker faker, Guid portfolioId)
    {
        faker.RuleFor(x => x.PortfolioId, portfolioId);

        return faker;
    }

    public static StrategicInitiativeFaker WithRoles(this StrategicInitiativeFaker faker, Dictionary<StrategicInitiativeRole, HashSet<Guid>>? roles)
    {
        faker.RuleFor("_roles", (_, initiative) =>
        {
            if (roles is null)
            {
                return new HashSet<RoleAssignment<StrategicInitiativeRole>>();
            }

            HashSet<RoleAssignment<StrategicInitiativeRole>> updatedRoles = [];
            foreach (var role in roles)
            {
                foreach (var employeeId in role.Value)
                {
                    updatedRoles.Add(new RoleAssignment<StrategicInitiativeRole>(initiative.Id, role.Key, employeeId));
                }
            }

            return updatedRoles;
        });

        return faker;
    }

    /// <summary>
    /// Creates a strategic initiative with the status of Proposed.
    /// </summary>
    /// <param name="faker"></param>
    /// <param name="dateTimeProvider"></param>
    /// <param name="portfolioId"></param>
    /// <returns></returns>
    public static StrategicInitiative AsProposed(
        this StrategicInitiativeFaker faker,
        TestingDateTimeProvider dateTimeProvider,
        Guid? portfolioId = null)
    {
        var start = dateTimeProvider.Now.Plus(Duration.FromDays(10)).InUtc().LocalDateTime.Date;
        var end = start.PlusDays(200);

        return faker
            .WithStatus(StrategicInitiativeStatus.Proposed)
            .WithDateRange(new LocalDateRange(start, end))
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    /// <summary>
    /// Creates a strategic initiative with the status of Approved.
    /// </summary>
    /// <param name="faker"></param>
    /// <param name="dateTimeProvider"></param>
    /// <param name="portfolioId"></param>
    /// <returns></returns>
    public static StrategicInitiative AsApproved(
        this StrategicInitiativeFaker faker,
        TestingDateTimeProvider dateTimeProvider,
        Guid? portfolioId = null)
    {
        var start = dateTimeProvider.Now.Plus(Duration.FromDays(10)).InUtc().LocalDateTime.Date;
        var end = start.PlusDays(200);

        return faker
            .WithStatus(StrategicInitiativeStatus.Approved)
            .WithDateRange(new LocalDateRange(start, end))
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    /// <summary>
    /// Creates a strategic initiative with the status of Active.
    /// </summary>
    /// <param name="faker"></param>
    /// <param name="dateTimeProvider"></param>
    /// <param name="portfolioId"></param>
    /// <returns></returns>
    public static StrategicInitiative AsActive(
        this StrategicInitiativeFaker faker,
        TestingDateTimeProvider dateTimeProvider,
        Guid? portfolioId = null)
    {
        var start = dateTimeProvider.Now.Plus(Duration.FromDays(-10)).InUtc().LocalDateTime.Date;
        var end = start.PlusDays(200);

        return faker
            .WithStatus(StrategicInitiativeStatus.Active)
            .WithDateRange(new LocalDateRange(start, end))
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    /// <summary>
    /// Creates a strategic initiative with the status of Completed.
    /// </summary>
    /// <param name="faker"></param>
    /// <param name="dateTimeProvider"></param>
    /// <param name="portfolioId"></param>
    /// <returns></returns>
    public static StrategicInitiative AsCompleted(
        this StrategicInitiativeFaker faker,
        TestingDateTimeProvider dateTimeProvider,
        Guid? portfolioId = null)
    {
        var start = dateTimeProvider.Now.Plus(Duration.FromDays(-200)).InUtc().LocalDateTime.Date;
        var end = start.PlusDays(100);

        return faker
            .WithStatus(StrategicInitiativeStatus.Completed)
            .WithDateRange(new LocalDateRange(start, end))
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    /// <summary>
    /// Creates a strategic initiative with the status of Cancelled.
    /// </summary>
    /// <param name="faker"></param>
    /// <param name="dateTimeProvider"></param>
    /// <param name="portfolioId"></param>
    /// <returns></returns>
    public static StrategicInitiative AsCancelled(
        this StrategicInitiativeFaker faker,
        TestingDateTimeProvider dateTimeProvider,
        Guid? portfolioId = null)
    {
        var start = dateTimeProvider.Now.Plus(Duration.FromDays(-200)).InUtc().LocalDateTime.Date;
        var end = start.PlusDays(100);

        return faker
            .WithStatus(StrategicInitiativeStatus.Cancelled)
            .WithDateRange(new LocalDateRange(start, end))
            .WithOptionalPortfolioId(portfolioId)
            .Generate();
    }

    private static StrategicInitiativeFaker WithOptionalPortfolioId(this StrategicInitiativeFaker faker, Guid? portfolioId)
    {
        return portfolioId.HasValue ? faker.WithPortfolioId(portfolioId.Value) : faker;
    }
}