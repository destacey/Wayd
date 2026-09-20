using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Fails a chosen save, so a test can put a fault where production put one.
/// </summary>
/// <remarks>
/// One <c>SaveChangesAsync</c> on <c>BaseDbContext</c> reaches the database more than once: the entities,
/// then the audit trail for anything carrying a database-generated key, then the activity log and outbox
/// envelope rows. Failing the second or third is what a test cannot arrange any other way, and is what the
/// entity write has to be rolled back by.
/// <para>
/// Registered on the shared host for every test, so it does nothing until <see cref="FailOn"/> arms it.
/// </para>
/// </remarks>
public sealed class SaveFaultInjector : SaveChangesInterceptor
{
    private Fault? _fault;

    /// <summary>Arms the injector to throw on the <paramref name="ordinal"/>th save from here, counting from one.</summary>
    public Fault FailOn(int ordinal)
    {
        var fault = new Fault(this, ordinal);
        _fault = fault;

        return fault;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _fault?.CountAndThrowWhenReached();

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public sealed class Fault(SaveFaultInjector owner, int ordinal) : IDisposable
    {
        private readonly SaveFaultInjector _owner = owner;
        private readonly int _ordinal = ordinal;
        private int _saves;

        /// <summary>The message the injected exception carries, so a test can prove it caught its own fault.</summary>
        public const string Message = "Injected save fault.";

        public bool WasReached { get; private set; }

        internal void CountAndThrowWhenReached()
        {
            if (++_saves != _ordinal)
                return;

            WasReached = true;
            throw new InvalidOperationException(Message);
        }

        public void Dispose() => _owner._fault = null;
    }
}
