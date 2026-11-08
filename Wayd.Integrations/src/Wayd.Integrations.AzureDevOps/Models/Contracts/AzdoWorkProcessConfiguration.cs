using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Integrations.AzureDevOps.Models.Contracts;

public sealed record AzdoWorkProcessConfiguration : AzdoWorkProcess, IExternalWorkProcessConfiguration
{
    public IList<IExternalWorkTypeLevel> WorkTypeLevels { get; set; } = [];
    public IList<IExternalWorkTypeWorkflow> WorkTypes { get; set; } = [];
}
