namespace Wayd.Common.Application.Interfaces.ExternalWork;

public interface IExternalWorkProcessConfiguration : IExternalWorkProcess
{
    IList<IExternalWorkTypeLevel> WorkTypeLevels { get; }
    IList<IExternalWorkTypeWorkflow> WorkTypes { get; }
}