using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectStatusHistoryFaker : PrivateConstructorFaker<ProjectStatusHistory>
{
    public ProjectStatusHistoryFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ProjectId, f => f.Random.Guid());
        RuleFor(x => x.FromStatus, f => ProjectStatus.Proposed);
        RuleFor(x => x.ToStatus, f => ProjectStatus.Active);
        RuleFor(x => x.ChangedByUserId, f => f.Random.Guid().ToString());
        RuleFor(x => x.ChangedByEmployeeId, f => f.Random.Guid());
        RuleFor(x => x.ChangedOn, f => Instant.FromUtc(2026, 5, 1, 0, 0));
        RuleFor(x => x.Source, f => ProjectStatusHistorySource.Recorded);
        RuleFor(x => x.Reason, f => null);
        RuleFor(x => x.Sequence, f => 1);
    }
}

public static class ProjectStatusHistoryFakerExtensions
{
    public static ProjectStatusHistoryFaker WithId(this ProjectStatusHistoryFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithProjectId(this ProjectStatusHistoryFaker faker, Guid projectId)
    {
        faker.RuleFor(x => x.ProjectId, projectId);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithFromStatus(this ProjectStatusHistoryFaker faker, ProjectStatus? fromStatus)
    {
        faker.RuleFor(x => x.FromStatus, fromStatus);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithToStatus(this ProjectStatusHistoryFaker faker, ProjectStatus toStatus)
    {
        faker.RuleFor(x => x.ToStatus, toStatus);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithChangedByUserId(this ProjectStatusHistoryFaker faker, string changedByUserId)
    {
        faker.RuleFor(x => x.ChangedByUserId, changedByUserId);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithChangedByEmployeeId(this ProjectStatusHistoryFaker faker, Guid? changedByEmployeeId)
    {
        faker.RuleFor(x => x.ChangedByEmployeeId, changedByEmployeeId);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithChangedByEmployee(this ProjectStatusHistoryFaker faker, Employee employee)
    {
        faker.RuleFor(x => x.ChangedByEmployeeId, employee.Id);
        faker.RuleFor(x => x.ChangedByEmployee, employee);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithChangedOn(this ProjectStatusHistoryFaker faker, Instant changedOn)
    {
        faker.RuleFor(x => x.ChangedOn, changedOn);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithSource(this ProjectStatusHistoryFaker faker, ProjectStatusHistorySource source)
    {
        faker.RuleFor(x => x.Source, source);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithReason(this ProjectStatusHistoryFaker faker, string? reason)
    {
        faker.RuleFor(x => x.Reason, reason);

        return faker;
    }

    public static ProjectStatusHistoryFaker WithSequence(this ProjectStatusHistoryFaker faker, int sequence)
    {
        faker.RuleFor(x => x.Sequence, sequence);

        return faker;
    }
}
