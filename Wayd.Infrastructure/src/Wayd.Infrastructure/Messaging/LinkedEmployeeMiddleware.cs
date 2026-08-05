using Wolverine;

namespace Wayd.Infrastructure.Messaging;

/// <summary>
/// Wolverine middleware that enforces <see cref="IRequireLinkedEmployee"/>: a message marked with it is
/// rejected before its handler runs unless the caller's account is linked to an employee record.
/// </summary>
/// <remarks>
/// This is the bridge between the two authorization systems. Permissions are checked against the user
/// (<c>MustHavePermission</c>); the domain checks assignments, managers, and membership against the
/// employee. Without a declared precondition, a user holding the right permission but no employee link
/// failed inside the handler in whatever way that handler happened to be written — a guard-clause 500,
/// a "could not determine employee id" failure, or silent emptiness. Those become one 403 carrying an
/// instruction the caller can act on.
/// <para>
/// Wolverine's generated <c>HandleAsync</c> calls middleware before it constructs the handler, so this
/// also short-circuits the Planning handlers that resolve the employee id in primary-constructor field
/// initializers — a throw during construction that no in-handler check could have intercepted.
/// </para>
/// <para>
/// Only <see cref="ActorKind.User"/> is gated. System scopes (scheduled jobs, durable message delivery,
/// startup work) are not people and hold no employee link, but they must still be able to dispatch these
/// messages — a background re-dispatch of a user-originated command would otherwise fail here.
/// </para>
/// </remarks>
public static class LinkedEmployeeMiddleware
{
    internal const string UnlinkedMessage =
        "Your account isn't linked to an employee record, which this action requires. "
        + "Ask an administrator to link your account in Settings → Users.";

    public static async Task Before(
        Envelope envelope,
        ICurrentUser currentUser,
        ICurrentPrincipal currentPrincipal,
        CancellationToken cancellationToken)
    {
        if (envelope.Message is not IRequireLinkedEmployee)
        {
            return;
        }

        if (currentUser.Kind != ActorKind.User)
        {
            return;
        }

        if (await currentPrincipal.GetEmployeeId(cancellationToken) is null)
        {
            throw new ForbiddenException(UnlinkedMessage);
        }
    }
}
