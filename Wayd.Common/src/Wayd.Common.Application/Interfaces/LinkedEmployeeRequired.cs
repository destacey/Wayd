using System.Diagnostics.CodeAnalysis;
using Wayd.Common.Application.Exceptions;

namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// The refusal issued when an action requires the caller's account to be linked to an employee
/// record and it is not.
/// </summary>
/// <remarks>
/// Enforced in two places on purpose, because neither alone is sufficient:
/// <list type="bullet">
///   <item><see cref="IRequireLinkedEmployee"/> plus its middleware <em>declares</em> the
///   precondition and rejects the request before the handler is built. That is what makes the
///   requirement discoverable, and it is the only thing that can stop a handler which resolves the
///   employee id while constructing.</item>
///   <item>The handler's own check is what actually <em>guarantees</em> it. Wolverine's code
///   generation requires handlers to be public, so any code holding the Application assembly can
///   construct one and call <c>Handle</c> directly, never passing through the middleware. A
///   precondition enforced only on the dispatch path is a convention, not an invariant.</item>
/// </list>
/// Handlers <see cref="Throw"/> rather than returning a failed <c>Result</c>: this is a refusal, not
/// a business outcome, and a caller that bypassed the middleware should not be able to fold it into
/// ordinary result handling. <c>ExceptionMiddleware</c> maps it to the same 403 and wording the
/// middleware produces, so the caller sees one explanation by either route.
/// </remarks>
public static class LinkedEmployeeRequired
{
    public const string Message =
        "Your account isn't linked to an employee record, which this action requires. "
        + "Ask an administrator to link your account in Settings → Users.";

    /// <summary>Refuses the action. Never returns.</summary>
    [DoesNotReturn]
    public static void Throw() => throw new ForbiddenException(Message);
}
