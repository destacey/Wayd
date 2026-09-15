using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Records every entity any context adds while armed, as EF sees it at the moment of saving.
/// </summary>
/// <remarks>
/// For proving what a rolled-back transaction wrote on the way. Reading the tables afterwards shows only
/// that nothing survived, which is also what a run that never wrote anything looks like.
/// <para>
/// Registered on the shared host for every test, so it records nothing until <see cref="Record"/> is called.
/// </para>
/// </remarks>
public sealed class SaveRecorder : SaveChangesInterceptor
{
    private ConcurrentQueue<object>? _added;

    /// <summary>Starts recording; the returned list is what was added until the handle is disposed.</summary>
    public Recording Record()
    {
        var added = new ConcurrentQueue<object>();
        _added = added;

        return new Recording(this, added);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_added is { } added && eventData.Context is not null)
        {
            foreach (var entry in eventData.Context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
                added.Enqueue(entry.Entity);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public sealed class Recording(SaveRecorder owner, ConcurrentQueue<object> added) : IDisposable
    {
        private readonly SaveRecorder _owner = owner;

        public IReadOnlyList<object> Added => [.. added];

        public IEnumerable<T> AddedOf<T>() => Added.OfType<T>();

        /// <summary>
        /// Wolverine's envelope rows naming a message type — how a durable event reaches its queue. Matched
        /// by name because the entity types are Wolverine's internals.
        /// </summary>
        public IEnumerable<string> EnvelopeMessageTypes() =>
            Added
                .Where(e => e.GetType().Namespace?.StartsWith("Wolverine", StringComparison.Ordinal) == true)
                .Select(e => e.GetType().GetProperty("MessageType")?.GetValue(e) as string)
                .OfType<string>();

        public void Dispose() => Interlocked.CompareExchange(ref _owner._added, null, added);
    }
}
