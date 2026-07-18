namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Handles an <see cref="ICommand"/> that yields a plain <see cref="Result"/>. The public
/// <c>Handle</c> method is what Wolverine's handler discovery binds to.
/// </summary>
/// <typeparam name="TCommand">The command type handled.</typeparam>
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles an <see cref="ICommand{TResponse}"/> that yields a <see cref="Result{TResponse}"/>.
/// The public <c>Handle</c> method is what Wolverine's handler discovery binds to.
/// </summary>
/// <typeparam name="TCommand">The command type handled.</typeparam>
/// <typeparam name="TResponse">The type carried by a successful result.</typeparam>
public interface ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
