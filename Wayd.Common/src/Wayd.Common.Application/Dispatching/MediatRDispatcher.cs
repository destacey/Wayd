using MediatR;

namespace Wayd.Common.Application.Dispatching;

/// <summary>
/// <see cref="IDispatcher"/> implementation backed by MediatR's <see cref="ISender"/>. This is the
/// only place in the application that depends on MediatR's dispatch surface; every other call site
/// goes through <see cref="IDispatcher"/>.
/// </summary>
internal sealed class MediatRDispatcher(ISender sender) : IDispatcher
{
    private readonly ISender _sender = sender;

    public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default)
        => _sender.Send(command, cancellationToken);

    public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        => _sender.Send(command, cancellationToken);

    public Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        => _sender.Send(query, cancellationToken);
}
