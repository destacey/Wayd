using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record WorkItemProgressDailyRollupDto : WorkItemProgressRollupDto
{
    public DateOnly Date { get; set; }

    public static WorkItemProgressDailyRollupDto Create(DateOnly date, int proposed, int active, int done)
    {
        return new WorkItemProgressDailyRollupDto
        {
            Date = date,
            Proposed = proposed,
            Active = active,
            Done = done
        };
    }

    public static WorkItemProgressDailyRollupDto CreateEmpty(DateOnly date)
    {
        return Create(date, 0, 0, 0);
    }

    public static List<WorkItemProgressDailyRollupDto> CreateList(DateOnly startDate, DateOnly endDate, List<WorkItemProgressStateDto> workItems)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date must be less than or equal to end date");

        var days = endDate.DayNumber - startDate.DayNumber + 1; // inclusive
        var data = Enumerable.Range(0, days)
            .Select(offset => CreateEmpty(startDate.AddDays(offset)))
            .ToList();

        var filteredWorkItems = workItems
            .Where(w => w.Tier == WorkTypeTier.Requirement
                && w.StatusCategory != StatusCategory.Removed) // temporary filter until we get the date from history
            .ToArray();

        foreach (var workItem in filteredWorkItems)
        {
            if ((workItem.StatusCategory is StatusCategory.Done or StatusCategory.Removed) && !workItem.DoneTimestamp.HasValue)
                throw new ArgumentException("Work item is in a terminal state but does not have a done timestamp");

            var created = workItem.Created.ToDateOnly();
            DateOnly? activatedTimestamp = workItem.ActivatedTimestamp.HasValue
                ? workItem.ActivatedTimestamp.Value.ToDateOnly()
                : null;
            DateOnly? doneTimestamp = workItem.DoneTimestamp.HasValue
                ? workItem.DoneTimestamp.Value.ToDateOnly()
                : null;

            int startIndex = Math.Max(0, created.DayNumber - startDate.DayNumber);
            for (int i = startIndex; i < days; i++)
            {
                if (workItem.StatusCategory == StatusCategory.Done && doneTimestamp <= data[i].Date)
                {
                    data[i].Done++;
                    continue;
                }
                // TODO: bring this back when we get the date from history
                //else if (workItem.StatusCategory == StatusCategory.Removed && doneTimestamp <= data[i].Date)
                //{
                //    // If the work item is removed before the end date, stop incrementing
                //    break;
                //}
                else if (activatedTimestamp.HasValue && activatedTimestamp <= data[i].Date)
                {
                    data[i].Active++;
                    continue;
                }
                data[i].Proposed++;
            }
        }

        return data;
    }
}
