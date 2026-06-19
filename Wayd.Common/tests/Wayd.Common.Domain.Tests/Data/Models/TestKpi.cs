using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;

namespace Wayd.Common.Domain.Tests.Data.Models;

/// <summary>
/// Concrete implementation of Kpi for testing purposes.
/// </summary>
public sealed class TestKpi : Kpi
{
    private TestKpi() : base() { }

    private TestKpi(string name, string? description, double? startingValue, double targetValue, string? prefix, string? suffix, KpiTargetDirection direction)
        : base(name, description, startingValue, targetValue, prefix, suffix, direction)
    {
    }

    public new Result Update(string name, string? description, double? startingValue, double targetValue, string? prefix, string? suffix, KpiTargetDirection direction)
    {
        return base.Update(name, description, startingValue, targetValue, prefix, suffix, direction);
    }

    public static TestKpi Create(string name, string? description, double? startingValue, double targetValue, string? prefix, string? suffix, KpiTargetDirection direction)
    {
        return new TestKpi(name, description, startingValue, targetValue, prefix, suffix, direction);
    }
}
