using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Interfaces.ExternalWork;

public interface IExternalWorkflowState
{
    string StatusName { get; }
    StatusCategory Category { get; }
    int Order { get; }
    bool IsActive { get; }
}
