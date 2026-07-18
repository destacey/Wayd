namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Dispatches commands and queries to their handlers. This is the single seam through which the
/// application talks to the underlying mediator, so the dispatcher implementation (Wolverine today)
/// can be swapped without touching call sites.
/// </summary>
/// <remarks>
/// The response type is inferred from the <see cref="ICommand"/>/<see cref="ICommand{TResponse}"/>/
/// <see cref="IQuery{TResponse}"/> markers, so every call site reads as a simple
/// <c>_dispatcher.Send(message, ct)</c>. No underlying mediator types appear in this contract, keeping
/// it implementable over any dispatcher; the current implementation (<c>WolverineDispatcher</c>) is a
/// thin pass-through to Wolverine's <c>IMessageBus.InvokeAsync&lt;T&gt;(object message)</c>.
/// </remarks>
public interface IDispatcher
{
    /// <summary>Dispatches a command that yields a plain <see cref="Result"/>.</summary>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of handling the command.</returns>
    Task<Result> Send(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>Dispatches a command that yields a <see cref="Result{TResponse}"/>.</summary>
    /// <typeparam name="TResponse">The type carried by a successful result.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of handling the command.</returns>
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>Dispatches a query that yields <typeparamref name="TResponse"/>.</summary>
    /// <typeparam name="TResponse">The type returned by the query.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The query response.</returns>
    Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
