namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Marks a component that coordinates several handlers to complete one piece of work.
/// </summary>
/// <remarks>
/// The tier above the handlers:
/// <code>
/// saga  →  handlers  →  service  →  DbContext / domain
/// </code>
/// A saga is the only tier allowed to hold <see cref="Dispatching.IDispatcher"/>. That is the whole
/// distinction from a service: a service does one job against the database and the domain and never
/// dispatches, so the dependency graph below a handler stays a tree and no cycle among services is
/// constructible. When a workflow needs several handlers run in order, it belongs here.
/// <para>
/// A saga <em>may</em> be a transaction boundary but is not required to be — the multi-handler
/// unit-of-work story is deliberately still open.
/// </para>
/// <para>
/// Registered as transient, like <see cref="ITransientService"/>. It extends that marker so the existing
/// DI scan keeps registering it with no change; the separate name is what the architecture tests key on
/// to allow dispatching here and forbid it in a service.
/// </para>
/// </remarks>
public interface ISaga : ITransientService
{
}
