using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// Marks every event raised by a KPI, and names the record the KPI is about.
/// </summary>
/// <remarks>
/// A KPI's activity belongs on its subject as well as on the KPI, so every KPI event relates the subject through
/// <see cref="IRelatedAggregateEvent"/>, filed under the activity type its subject type declares. A module's handlers
/// react only to KPIs on their own records, so they filter on <see cref="SubjectType"/> without loading the KPI.
/// </remarks>
public interface IKpiEvent : IEvent, IRelatedAggregateEvent
{
    /// <summary>The registered subject type of the KPI, such as <c>organization.team</c>.</summary>
    string SubjectType { get; }

    /// <summary>The record the KPI is about.</summary>
    Guid SubjectId { get; }
}

/// <summary>Works out the records a KPI event is filed under besides the KPI.</summary>
internal static class KpiEventRelations
{
    /// <summary>
    /// The subject, by the activity type its subject type declares, or nothing when the subject type is no longer
    /// registered.
    /// </summary>
    public static IReadOnlyCollection<AggregateReference> For(string subjectType, Guid subjectId)
    {
        var descriptor = KpiSubjectTypes.Resolve(subjectType);
        return descriptor.IsSuccess
            ? [new AggregateReference(descriptor.Value.ActivityAggregateType, subjectId)]
            : [];
    }
}
