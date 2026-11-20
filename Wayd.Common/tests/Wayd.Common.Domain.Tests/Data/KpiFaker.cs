using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

/// <summary>
/// An active, manual KPI on a test subject, aiming to raise a baseline of 10 to 20, with no measurements.
/// </summary>
public sealed class KpiFaker : PrivateConstructorFaker<Kpi>
{
    /// <summary>The subject type every faked KPI is on, registered when the faker is built.</summary>
    public static readonly KpiSubjectTypeDescriptor TestSubject = new("test.kpis.subject", "Gadget", "Gadget");

    public KpiFaker()
    {
        KpiSubjectTypes.Register(TestSubject);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.SubjectType, TestSubject.Key);
        RuleFor(x => x.SubjectId, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Direction, KpiTargetDirection.Increase);
        RuleFor("_baselineValue", _ => (double?)10);
        RuleFor("_targetKind", _ => KpiTargetKind.Absolute);
        RuleFor("_targetValue", _ => 20d);
        RuleFor(x => x.Source, KpiSource.Manual);
        RuleFor(x => x.ExpressedAs, KpiExpressedAs.Value);
        RuleFor(x => x.Status, KpiStatus.Active);
        RuleFor(x => x.Order, 0);
        // A KPI as loaded with its checkpoint plan, which every plan and goal change needs.
        RuleFor("_checkpoints", _ => new List<KpiCheckpoint>());
    }
}

public static class KpiFakerExtensions
{
    /// <summary>A KPI as a query that did not include its checkpoint plan loads it.</summary>
    public static KpiFaker WithUnloadedCheckpoints(this KpiFaker faker)
    {
        faker.RuleFor("_checkpoints", _ => (List<KpiCheckpoint>?)null);
        return faker;
    }

    public static KpiFaker WithSubjectId(this KpiFaker faker, Guid subjectId)
    {
        faker.RuleFor(x => x.SubjectId, subjectId);
        return faker;
    }

    public static KpiFaker WithDirection(this KpiFaker faker, KpiTargetDirection direction)
    {
        faker.RuleFor(x => x.Direction, direction);
        return faker;
    }

    public static KpiFaker WithBaseline(this KpiFaker faker, KpiBaseline baseline)
    {
        faker.RuleFor("_baselineValue", _ => baseline.Value);
        faker.RuleFor("_baselineWindowStart", _ => baseline.Window?.Start);
        faker.RuleFor("_baselineWindowEnd", _ => baseline.Window?.End);
        return faker;
    }

    public static KpiFaker WithTarget(this KpiFaker faker, KpiTarget target)
    {
        faker.RuleFor("_targetKind", _ => target.Kind);
        faker.RuleFor("_targetValue", _ => target.Value);
        return faker;
    }

    public static KpiFaker WithAtRisk(this KpiFaker faker, KpiAtRiskThreshold? atRisk)
    {
        faker.RuleFor("_atRiskKind", _ => atRisk?.Kind);
        faker.RuleFor("_atRiskValue", _ => atRisk?.Value);
        return faker;
    }

    public static KpiFaker WithStatus(this KpiFaker faker, KpiStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static KpiFaker WithOrder(this KpiFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);
        return faker;
    }
}
