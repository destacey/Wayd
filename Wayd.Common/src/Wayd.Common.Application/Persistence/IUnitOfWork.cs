namespace Wayd.Common.Application.Persistence;

/// <summary>
/// A transaction over several saves, for a change the database must see in a set order: each save reaches the
/// database in turn, yet they commit together and deliver what they raised once. Commit through it; disposing
/// without committing rolls every save back.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
