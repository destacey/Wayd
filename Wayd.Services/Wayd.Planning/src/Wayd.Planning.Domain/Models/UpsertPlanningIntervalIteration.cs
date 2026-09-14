using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Domain.Models;

public sealed class UpsertPlanningIntervalIteration
{
    public Guid? Id { get; set; }
    public required string Name { get; set; }
    public IterationCategory Category { get; set; }
    public required LocalDateRange DateRange { get; set; }
    public bool IsNew => Id.IsNullEmptyOrDefault();

    public static UpsertPlanningIntervalIteration Create(Guid? id, string name, IterationCategory category, LocalDateRange dateRange)
    {
        return new UpsertPlanningIntervalIteration
        {
            Id = id,
            Name = name,
            Category = category,
            DateRange = dateRange
        };
    }
}
