using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Tests.Shared.Data;
using NodaTime.Extensions;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public sealed class RiskFaker : PrivateConstructorFaker<Risk>
{
    public RiskFaker()
    {
        var now = DateTime.UtcNow.ToInstant();

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Summary, f => f.Lorem.Sentence());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.TeamId, f => f.Random.Guid());
        RuleFor(x => x.ReportedOn, now);
        RuleFor(x => x.ReportedById, f => f.Random.Guid());
        RuleFor(x => x.Status, RiskStatus.Open);
        RuleFor(x => x.Category, f => f.PickRandom<RiskCategory>());
        RuleFor(x => x.Impact, f => f.PickRandom<RiskGrade>());
        RuleFor(x => x.Likelihood, f => f.PickRandom<RiskGrade>());
        RuleFor(x => x.AssigneeId, f => f.Random.Guid());
        RuleFor(x => x.FollowUpDate, f => f.Date.Future().ToLocalDateTime().Date);
        RuleFor(x => x.Response, f => f.Lorem.Sentence());
        RuleFor(x => x.ClosedDate, (Instant?)null);
    }
}

public static class RiskFakerExtensions
{
    public static RiskFaker WithId(this RiskFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static RiskFaker WithKey(this RiskFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static RiskFaker WithSummary(this RiskFaker faker, string summary)
    {
        faker.RuleFor(x => x.Summary, summary);
        return faker;
    }

    public static RiskFaker WithDescription(this RiskFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static RiskFaker WithTeamId(this RiskFaker faker, Guid? teamId)
    {
        faker.RuleFor(x => x.TeamId, teamId);
        return faker;
    }

    public static RiskFaker WithReportedOn(this RiskFaker faker, Instant reportedOn)
    {
        faker.RuleFor(x => x.ReportedOn, reportedOn);
        return faker;
    }

    public static RiskFaker WithReportedById(this RiskFaker faker, Guid reportedById)
    {
        faker.RuleFor(x => x.ReportedById, reportedById);
        return faker;
    }

    public static RiskFaker WithStatus(this RiskFaker faker, RiskStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static RiskFaker WithCategory(this RiskFaker faker, RiskCategory category)
    {
        faker.RuleFor(x => x.Category, category);
        return faker;
    }

    public static RiskFaker WithImpact(this RiskFaker faker, RiskGrade impact)
    {
        faker.RuleFor(x => x.Impact, impact);
        return faker;
    }

    public static RiskFaker WithLikelihood(this RiskFaker faker, RiskGrade likelihood)
    {
        faker.RuleFor(x => x.Likelihood, likelihood);
        return faker;
    }

    public static RiskFaker WithAssigneeId(this RiskFaker faker, Guid? assigneeId)
    {
        faker.RuleFor(x => x.AssigneeId, assigneeId);
        return faker;
    }

    public static RiskFaker WithFollowUpDate(this RiskFaker faker, LocalDate? followUpDate)
    {
        faker.RuleFor(x => x.FollowUpDate, followUpDate);
        return faker;
    }

    public static RiskFaker WithResponse(this RiskFaker faker, string? response)
    {
        faker.RuleFor(x => x.Response, response);
        return faker;
    }

    public static RiskFaker AsOpen(this RiskFaker faker)
    {
        faker.RuleFor(x => x.Status, RiskStatus.Open);
        faker.RuleFor(x => x.ClosedDate, (Instant?)null);
        return faker;
    }

    public static RiskFaker AsClosed(this RiskFaker faker, Instant? closedDate = null)
    {
        var closeDate = closedDate ?? SystemClock.Instance.GetCurrentInstant();
        faker.RuleFor(x => x.Status, RiskStatus.Closed);
        faker.RuleFor(x => x.ClosedDate, closeDate);
        return faker;
    }

    public static RiskFaker AsHighImpact(this RiskFaker faker)
    {
        faker.RuleFor(x => x.Impact, RiskGrade.High);
        return faker;
    }

    public static RiskFaker AsHighLikelihood(this RiskFaker faker)
    {
        faker.RuleFor(x => x.Likelihood, RiskGrade.High);
        return faker;
    }

    public static RiskFaker AsLowRisk(this RiskFaker faker)
    {
        faker.RuleFor(x => x.Impact, RiskGrade.Low);
        faker.RuleFor(x => x.Likelihood, RiskGrade.Low);
        return faker;
    }
}
