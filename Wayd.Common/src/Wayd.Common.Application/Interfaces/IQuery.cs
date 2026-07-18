namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Marker for a query that yields <typeparamref name="TResponse"/>. Dispatched through
/// <see cref="IDispatcher"/>; the underlying mediator (Wolverine) routes it to the matching
/// handler by concrete type. This is a pure marker — it intentionally carries no dependency on
/// any mediator library.
/// </summary>
/// <typeparam name="TResponse">The type returned by the query.</typeparam>
public interface IQuery<TResponse>
{
}
