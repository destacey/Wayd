namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// The scopes a record sits in, most specific first, that workflow lookup walks before falling back to the
/// organization — for example a project, its program, then its portfolio.
/// </summary>
/// <remarks>
/// The owning module builds the chain, because only it knows what its scope ids mean and how its containers
/// nest; the engine only walks it. At each level the record type's entry is tried, then that level's default,
/// so a narrower scope's default beats a broader scope's type entry. The organization is never listed: it is
/// always the last level, and its assignment the mandatory last resort.
/// <para>
/// A level the record does not have — a project with no program — is left out rather than passed empty.
/// </para>
/// </remarks>
public sealed class WorkflowScopeChain
{
    private readonly Guid[] _scopeIds;

    private WorkflowScopeChain(Guid[] scopeIds) => _scopeIds = scopeIds;

    /// <summary>The chain with no scope of its own: only the organization is consulted.</summary>
    public static WorkflowScopeChain Organization { get; } = new([]);

    /// <summary>
    /// The scope ids, most specific first, without the organization.
    /// </summary>
    public IReadOnlyList<Guid> ScopeIds => Array.AsReadOnly(_scopeIds);

    /// <summary>
    /// A chain of one scope, or <see cref="Organization"/> when <paramref name="scopeId"/> is <c>null</c> — for an
    /// owner type with a single level of scope.
    /// </summary>
    public static WorkflowScopeChain ForScope(Guid? scopeId) =>
        scopeId is { } id ? Of(id) : Organization;

    /// <summary>A chain of the given scopes, most specific first.</summary>
    /// <exception cref="ArgumentException">A scope id is empty or listed twice.</exception>
    public static WorkflowScopeChain Of(params IEnumerable<Guid> scopeIds)
    {
        ArgumentNullException.ThrowIfNull(scopeIds);

        List<Guid> ids = [.. scopeIds];

        if (ids.Contains(Guid.Empty))
        {
            throw new ArgumentException("A scope id cannot be empty; leave out a level the record does not have.", nameof(scopeIds));
        }

        if (ids.Distinct().Count() != ids.Count)
        {
            throw new ArgumentException("A scope cannot appear twice in one chain.", nameof(scopeIds));
        }

        return ids.Count == 0 ? Organization : new WorkflowScopeChain([.. ids]);
    }

    /// <summary>
    /// Where <paramref name="scopeId"/> sits in the chain, 0 being the most specific; the organization
    /// (<c>null</c>) and any scope not in the chain come after every listed scope.
    /// </summary>
    public int Rank(Guid? scopeId)
    {
        if (scopeId is not { } id)
        {
            return _scopeIds.Length;
        }

        var index = Array.IndexOf(_scopeIds, id);
        return index < 0 ? int.MaxValue : index;
    }
}
