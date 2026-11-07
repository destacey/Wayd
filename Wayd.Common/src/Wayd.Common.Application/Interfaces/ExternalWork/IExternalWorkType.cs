namespace Wayd.Common.Application.Interfaces.ExternalWork;

public interface IExternalWorkType
{
    /// <summary>
    /// The source's stable identifier for the work type within its process (an Azure DevOps reference
    /// name), which does not change when the type is renamed.
    /// </summary>
    string ExternalKey { get; }

    string Name { get; }
    string? Description { get; }
    string WorkTypeLevelId { get; }
    bool IsActive { get; }
}
