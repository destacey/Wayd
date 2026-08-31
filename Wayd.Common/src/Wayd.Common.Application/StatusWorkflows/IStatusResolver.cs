using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Loads the workflow governing a scope and hands back a status ready for an aggregate to store.
/// </summary>
/// <remarks>
/// <para>
/// Every aggregate method that changes status takes a <see cref="StatusRef"/> the caller must supply,
/// because the domain has no I/O and cannot fetch one. This is the other side of that contract: the
/// two queries — find the assignment, load its workflow — that turn "the Released status" into
/// something a handler can pass in.
/// </para>
/// <para>
/// An application service, not a domain one: its entire job is loading. The decision it delegates to —
/// <see cref="StatusWorkflow.StatusFor"/> — stays on the aggregate, and this must never grow rules
/// about <em>which</em> status is appropriate. The caller names the alias, because the caller is the
/// one whose method signature demands it.
/// </para>
/// <para>
/// A service may depend on the database, the domain, and framework primitives — never on another
/// service and never on the dispatcher, so the dependency graph stays a tree and no cycle is
/// constructible. Coordination across several handlers belongs in a saga, above them.
/// </para>
/// </remarks>
public interface IStatusResolver : IScopedService
{
    /// <summary>
    /// Resolves the status carrying a well-known meaning in the workflow governing a scope.
    /// </summary>
    /// <param name="ownerType">The registered owner type, e.g. <c>delivery.release</c>.</param>
    /// <param name="scopeId">
    /// The scope, or <c>null</c> for the organization-level assignment. Product Management has no
    /// container yet and always passes <c>null</c>; Project Portfolio Management will pass a portfolio.
    /// </param>
    /// <param name="alias">The meaning to resolve, cast from the module's own alias enum.</param>
    /// <returns>
    /// A failure when the scope has no assignment, the workflow is missing, or no status carries that
    /// alias — all of which are misconfiguration a caller should surface rather than throw on.
    /// </returns>
    Task<Result<StatusRef>> ForAlias(string ownerType, Guid? scopeId, int alias, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the status a newly created record starts in.
    /// </summary>
    Task<Result<StatusRef>> Initial(string ownerType, Guid? scopeId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the whole workflow governing a scope, for the rarer callers that need more than one status
    /// — building a remap, or presenting every status a record could move to.
    /// </summary>
    Task<Result<StatusWorkflow>> ForScope(string ownerType, Guid? scopeId, CancellationToken cancellationToken);
}
