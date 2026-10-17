namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Represents a contract for a long-running request operation.  Requests implementing this interface will be ignored by the <see cref="Behaviors.PerformanceBehavior"/> slow-message warning.
/// </summary>
public interface ILongRunningRequest
{
}
