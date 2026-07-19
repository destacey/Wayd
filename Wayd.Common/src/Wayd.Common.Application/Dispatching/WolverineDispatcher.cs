using Wolverine;

namespace Wayd.Common.Application.Dispatching;

/// <summary>
/// <see cref="IDispatcher"/> implementation backed by Wolverine's <see cref="IMessageBus"/>. This is
/// the only place in the application that depends on Wolverine's dispatch surface; every other call
/// site goes through <see cref="IDispatcher"/>.
/// </summary>
/// <remarks>
/// Each overload calls <c>IMessageBus.InvokeAsync&lt;T&gt;(object, ...)</c>, passing the command/query
/// typed only as <see cref="object"/> — Wolverine routes on the concrete runtime type. The current
/// user id is stamped onto the outgoing envelope so it survives Wolverine's fresh-per-message DI scope
/// (see <c>UserIdentityMiddleware</c>); without it, sends originating outside an HTTP request
/// (Hangfire jobs) would lose audit attribution.
/// </remarks>
internal sealed class WolverineDispatcher(IMessageBus bus, ICurrentUser currentUser) : IDispatcher
{
    private readonly IMessageBus _bus = bus;
    private readonly ICurrentUser _currentUser = currentUser;

    public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default)
    {
        var options = UserDeliveryOptions();
        return options is null
            ? _bus.InvokeAsync<Result>(command, cancellationToken)
            : _bus.InvokeAsync<Result>(command, options, cancellationToken);
    }

    public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        var options = UserDeliveryOptions();
        return options is null
            ? _bus.InvokeAsync<Result<TResponse>>(command, cancellationToken)
            : _bus.InvokeAsync<Result<TResponse>>(command, options, cancellationToken);
    }

    public Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        var options = UserDeliveryOptions();
        return options is null
            ? _bus.InvokeAsync<TResponse>(query, cancellationToken)
            : _bus.InvokeAsync<TResponse>(query, options, cancellationToken);
    }

    /// <summary>
    /// Builds <see cref="DeliveryOptions"/> carrying the current user id header when one is available,
    /// so the handler's fresh scope can restore it. Returns <c>null</c> when there is no user, letting
    /// the caller use the plain <c>InvokeAsync</c> overload — behaviourally identical to before for the
    /// anonymous/system path.
    /// </summary>
    private DeliveryOptions? UserDeliveryOptions()
    {
        // System scopes are self-identifying — a handler scope with no HTTP context and no user header
        // already resolves to ActorKind.System — so propagating the system id would be redundant. The
        // header exists solely to carry a real acting user across the scope boundary.
        if (_currentUser.Kind == ActorKind.System)
        {
            return null;
        }

        var userId = _currentUser.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var options = new DeliveryOptions();
        options.Headers[UserIdentityHeaders.UserId] = userId;
        return options;
    }
}
