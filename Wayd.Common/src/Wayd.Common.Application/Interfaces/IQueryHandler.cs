namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Handles an <see cref="IQuery{TResponse}"/> that yields <typeparamref name="TResponse"/>. The
/// public <c>Handle</c> method is what Wolverine's handler discovery binds to.
/// </summary>
/// <typeparam name="TQuery">The query type handled.</typeparam>
/// <typeparam name="TResponse">The type returned by the query.</typeparam>
public interface IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
