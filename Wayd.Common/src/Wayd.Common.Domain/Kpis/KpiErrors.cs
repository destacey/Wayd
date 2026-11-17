namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// The failures KPIs report, shared by the domain, the handlers and their tests.
/// </summary>
public static class KpiErrors
{
    /// <summary>No subject type was given.</summary>
    public const string SubjectTypeRequired = "A KPI subject type is required.";

    /// <summary>A subject type no module registered.</summary>
    public static string UnknownSubjectType(string key) => $"'{key}' is not a kind of record that can carry KPIs.";

    /// <summary>A number that is NaN or infinite.</summary>
    public static string NotAFiniteNumber(string field) => $"The {field} must be a finite number.";

    /// <summary>A period that ends before it starts.</summary>
    public const string PeriodEndsBeforeStart = "A period must end on or after the day it starts.";

    /// <summary>A baseline window given without a baseline value.</summary>
    public const string BaselineWindowWithoutValue = "A baseline window needs a baseline value.";

    /// <summary>A relative target, or an at-risk threshold stated as progress, on a KPI with no baseline.</summary>
    public const string BaselineRequired = "A target or at-risk threshold stated relative to the baseline needs a baseline value.";

    /// <summary>A relative target from a baseline of zero, which resolves to the baseline itself.</summary>
    public const string RelativeChangeFromZero = "A percentage change from a baseline of zero is always zero. State the target as a value instead.";

    /// <summary>A relative target with no positive change.</summary>
    public const string RelativeChangeMustBePositive = "A percentage change must be greater than zero; the KPI's direction says which way it goes.";

    /// <summary>A baseline that already meets the target.</summary>
    public const string BaselineMeetsTargetIncrease = "The baseline must be below the target when the KPI should increase.";

    /// <summary>A baseline that already meets the target.</summary>
    public const string BaselineMeetsTargetDecrease = "The baseline must be above the target when the KPI should decrease.";

    /// <summary>An at-risk threshold stated as progress outside 0–100.</summary>
    public const string AtRiskProgressOutOfRange = "An at-risk threshold stated as progress must be between 0 and 100 percent, exclusive.";

    /// <summary>An at-risk threshold that is not short of the target.</summary>
    public const string AtRiskNotShortOfTargetIncrease = "The at-risk threshold must be below the target when the KPI should increase.";

    /// <summary>An at-risk threshold that is not short of the target.</summary>
    public const string AtRiskNotShortOfTargetDecrease = "The at-risk threshold must be above the target when the KPI should decrease.";

    /// <summary>Changing the measurements of a closed KPI.</summary>
    public const string KpiClosed = "This KPI is closed. Reopen it to change its measurements.";

    /// <summary>Closing a KPI that is already closed.</summary>
    public const string AlreadyClosed = "This KPI is already closed.";

    /// <summary>Reopening a KPI that is not closed.</summary>
    public const string NotClosed = "This KPI is not closed.";

    /// <summary>A measurement period that overlaps another on the same KPI.</summary>
    public const string PeriodOverlaps = "This period overlaps another measurement on the KPI. Each period can hold one measurement.";

    /// <summary>A measurement id that is not on the KPI.</summary>
    public const string MeasurementNotFound = "The measurement was not found on this KPI.";

    /// <summary>A negative sample size.</summary>
    public const string SampleSizeNegative = "A sample size cannot be negative.";

    /// <summary>A covered record given only by kind or only by id.</summary>
    public const string PeriodRecordIncomplete = "A period that covers a sprint, planning interval or release needs both the kind and the id.";

    /// <summary>A reorder whose list is not exactly the subject's KPIs.</summary>
    public const string ReorderMismatch = "The new order must list every KPI on the subject exactly once.";
}
