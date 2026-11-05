using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Extensions;

public static class DependencyStateExtensions
{
    public static DependencyState FromStatusCategoryString(string statusCategory)
    {
        return statusCategory switch
        {
            "Proposed" => DependencyState.ToDo,
            "Active" => DependencyState.InProgress,
            "Done" => DependencyState.Done,
            "Removed" => DependencyState.Done,
            _ => throw new ArgumentOutOfRangeException(nameof(statusCategory), statusCategory, null)
        };
    }

    public static StatusCategory ToStatusCategory(this DependencyState state)
    {
        return state switch
        {
            DependencyState.ToDo => StatusCategory.Proposed,
            DependencyState.InProgress => StatusCategory.Active,
            DependencyState.Done => StatusCategory.Done,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
