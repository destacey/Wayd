using Ardalis.GuardClauses;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// What a module declares about one kind of record it wants workflows for: how the record is
/// identified, what it is called, which well-known meanings its aggregates cannot function without,
/// and how to name one of those meanings in an error message.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam that keeps the engine free of any module's vocabulary. The alternative — an
/// enum of owner types in Common with a <c>switch</c> mapping each to its required aliases — puts
/// every module's terminology in one shared file that every module then has to edit. That file becomes
/// a union nobody owns, and the engine ends up referencing <c>ProductStatusAlias</c>,
/// <c>PpmStatusAlias</c> and whatever comes next, which is precisely the coupling per-module alias
/// enums exist to avoid.
/// </para>
/// <para>
/// A module declares its descriptors in its own domain project and registers them with
/// <see cref="WorkflowOwners"/>. Adding Project Portfolio Management or Goals to the engine is then a
/// new file in that module and no change here at all.
/// </para>
/// <para>
/// The trade against an enum is compile-time safety: <see cref="Key"/> is a string, so a typo is a
/// startup failure rather than a build error. That is why registration is validated eagerly and why
/// modules are expected to expose their descriptors as static readonly fields rather than writing the
/// key at each call site.
/// </para>
/// <para>
/// Not a record: a delegate and a collection compare by reference, so value equality would be false for
/// two descriptors of the same owner type and <see cref="WorkflowOwners"/>' identity check would break.
/// </para>
/// </remarks>
public sealed class WorkflowOwnerDescriptor
{
    /// <param name="key">
    /// Stable identifier, persisted on every workflow row. Namespaced by module — <c>product.release</c>
    /// — so two modules cannot collide on a common word like "project". <strong>Never change one after
    /// it ships</strong>: it is stored data, and renaming it orphans every workflow that carries it.
    /// </param>
    /// <param name="displayName">What to call this kind of record in a message or an admin screen.</param>
    /// <param name="requiredAliases">
    /// The well-known meanings this owner type's aggregates resolve by alias, and therefore cannot
    /// function without. A workflow missing any of them is refused at activation.
    /// </param>
    /// <param name="describeAlias">
    /// Renders one of this module's alias values as a name, for error messages. The engine stores
    /// aliases as plain integers and cannot name them itself.
    /// </param>
    public WorkflowOwnerDescriptor(
        string key,
        string displayName,
        IReadOnlyCollection<int> requiredAliases,
        Func<int, string> describeAlias)
    {
        Key = Guard.Against.NullOrWhiteSpace(key, nameof(key)).Trim();
        DisplayName = Guard.Against.NullOrWhiteSpace(displayName, nameof(displayName)).Trim();
        RequiredAliases = Guard.Against.Null(requiredAliases, nameof(requiredAliases));
        DescribeAlias = Guard.Against.Null(describeAlias, nameof(describeAlias));

        if (requiredAliases.Contains(StatusWorkflow.NoAlias))
        {
            throw new ArgumentException(
                $"'{key}' lists NoAlias as required. NoAlias means a status carries no well-known meaning, so requiring it is not satisfiable.",
                nameof(requiredAliases));
        }

        if (requiredAliases.Distinct().Count() != requiredAliases.Count)
        {
            throw new ArgumentException($"'{key}' lists the same required alias more than once.", nameof(requiredAliases));
        }
    }

    /// <summary>
    /// Stable identifier persisted on every workflow row. See the constructor for why it must never
    /// change once it has shipped.
    /// </summary>
    public string Key { get; }

    /// <summary>What to call this kind of record in a message or an admin screen.</summary>
    public string DisplayName { get; }

    /// <summary>
    /// The aliases a workflow for this owner type must supply before it can be activated.
    /// </summary>
    /// <remarks>
    /// Enforced at activation rather than at first use: a workflow missing an alias its aggregates need
    /// would otherwise fail later, deep inside a domain method, on a record an administrator has
    /// already created. Refusing activation turns that into an error while it can still be fixed.
    /// </remarks>
    public IReadOnlyCollection<int> RequiredAliases { get; }

    /// <summary>
    /// Names one of this module's alias values. Supplied by the module because the engine stores
    /// aliases as integers and has no way to name them.
    /// </summary>
    public Func<int, string> DescribeAlias { get; }
}
