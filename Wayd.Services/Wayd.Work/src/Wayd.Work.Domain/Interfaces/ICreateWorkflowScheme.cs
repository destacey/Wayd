using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Domain.Interfaces;

public interface ICreateWorkflowScheme
{
    int WorkStatusId { get; }
    StatusCategory Category { get; }
    int Order { get; }
    bool IsActive { get; }
}
