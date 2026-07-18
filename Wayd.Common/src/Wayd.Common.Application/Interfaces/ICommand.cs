namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Marker for a command that yields a plain <see cref="Result"/>. Dispatched through
/// <see cref="IDispatcher"/>; the underlying mediator (Wolverine) routes it to the matching
/// handler by concrete type. This is a pure marker — it intentionally carries no dependency on
/// any mediator library.
/// </summary>
public interface ICommand
{
}

/// <summary>
/// Marker for a command that yields a <see cref="Result{TResponse}"/>. See <see cref="ICommand"/>.
/// </summary>
/// <typeparam name="TResponse">The type carried by a successful result.</typeparam>
public interface ICommand<TResponse>
{
}
