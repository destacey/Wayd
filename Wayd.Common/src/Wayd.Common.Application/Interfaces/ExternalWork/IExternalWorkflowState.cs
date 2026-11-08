using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Interfaces.ExternalWork;

public interface IExternalWorkflowState
{
    /// <summary>
    /// The source's stable identifier for the state within its work type (an Azure DevOps state id), which
    /// does not change when the state is renamed.
    /// </summary>
    string ExternalKey { get; }

    string StatusName { get; }
    StatusCategory Category { get; }
    int Order { get; }
    bool IsActive { get; }
}
