using Ardalis.GuardClauses;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// What a module declares about one kind of record it wants workflows for: how the record is
/// identified, what it is called, the well-known meanings its statuses can carry and the category each
/// belongs to, and which meanings and categories its aggregates cannot function without.
/// </summary>
/// <remarks>
/// The seam that keeps the engine free of any module's vocabulary. A module declares its descriptors in
/// its own domain project and registers them with <see cref="WorkflowOwners"/>, so adding Project
/// Portfolio Management or Planning to the engine is a new file in that module and no change here.
/// <para>
/// <see cref="Key"/> is a string rather than an enum value, so a typo is a startup failure rather than
/// a build error. Registration is validated eagerly for that reason, and modules expose their
/// descriptors as static readonly fields rather than writing the key at each call site.
/// </para>
/// <para>
/// Not a record: <see cref="Aliases"/> compares by reference, so value equality would be false for two
/// descriptors of the same owner type and <see cref="WorkflowOwners"/>' identity check would break.
/// </para>
/// </remarks>
public sealed class WorkflowOwnerDescriptor
{
    private readonly Dictionary<int, string> _aliases;
    private readonly Dictionary<int, StatusCategory> _aliasCategories;
    private readonly Dictionary<StatusCategory, string> _categoryLabels;

    /// <param name="key">
    /// Stable identifier, persisted on every workflow row. Namespaced by module — <c>product.release</c>
    /// — so two modules cannot collide on a common word like "project". <strong>Never change one after
    /// it ships</strong>: it is stored data, and renaming it orphans every workflow that carries it.
    /// </param>
    /// <param name="displayName">What to call this kind of record in a message or an admin screen.</param>
    /// <param name="aliases">
    /// Every well-known meaning a status of this owner type can carry, keyed by value, each with its name
    /// and the one category a status carrying it belongs to. The names make the engine's error messages
    /// and an <c>int</c> column in a query readable; the values are persisted, so <strong>never renumber
    /// one</strong>.
    /// </param>
    /// <param name="requiredAliases">
    /// The subset of <paramref name="aliases"/> this owner type's aggregates resolve by alias, and
    /// therefore cannot function without. A workflow missing any of them is refused at activation.
    /// </param>
    /// <param name="requiredCategories">
    /// The categories a workflow for this owner type must have at least one status in. See
    /// <see cref="RequiredCategories"/>.
    /// </param>
    /// <param name="supportsRestrictedTransitions">
    /// Whether this owner type's records may be governed by a restricted transition set. Declare it only
    /// when every way the aggregate changes status can honour an arbitrary set of allowed moves, or when
    /// <paramref name="moves"/> declares the moves it cannot do without.
    /// </param>
    /// <param name="moves">
    /// For an owner type whose aggregates change status only through named methods, every move those
    /// methods make, each required or optional. Empty for an owner type that moves to any status it is
    /// given. See <see cref="Moves"/>.
    /// </param>
    /// <param name="featureFlag">
    /// The feature flag the owning module is released behind, or null when it is always available. See
    /// <see cref="FeatureFlag"/>.
    /// </param>
    /// <param name="categoryLabels">
    /// What this owner type calls some or all of the categories, in its own words. A category left out
    /// keeps its enum name. See <see cref="CategoryLabels"/>.
    /// </param>
    public WorkflowOwnerDescriptor(
        string key,
        string displayName,
        IReadOnlyDictionary<int, WorkflowAlias> aliases,
        IReadOnlyCollection<int> requiredAliases,
        IReadOnlyCollection<StatusCategory>? requiredCategories = null,
        bool supportsRestrictedTransitions = false,
        IReadOnlyCollection<WorkflowMove>? moves = null,
        string? featureFlag = null,
        IReadOnlyDictionary<StatusCategory, string>? categoryLabels = null)
    {
        Key = Guard.Against.NullOrWhiteSpace(key, nameof(key)).Trim();
        DisplayName = Guard.Against.NullOrWhiteSpace(displayName, nameof(displayName)).Trim();
        FeatureFlag = string.IsNullOrWhiteSpace(featureFlag) ? null : featureFlag.Trim();
        Guard.Against.Null(aliases, nameof(aliases));
        RequiredAliases = Guard.Against.Null(requiredAliases, nameof(requiredAliases));
        SupportsRestrictedTransitions = supportsRestrictedTransitions;

        if (aliases.ContainsKey(StatusWorkflow.NoAlias))
        {
            throw new ArgumentException(
                $"'{key}' declares a name for NoAlias, which means a status carries no well-known meaning.",
                nameof(aliases));
        }

        if (aliases.Values.Any(a => a is null || string.IsNullOrWhiteSpace(a.Name)))
        {
            throw new ArgumentException($"'{key}' declares an alias with no name.", nameof(aliases));
        }

        if (aliases.Values.Any(a => !Enum.IsDefined(a.Category)))
        {
            throw new ArgumentException($"'{key}' declares an alias with no category.", nameof(aliases));
        }

        var duplicateName = aliases.Values
            .Select(a => a.Name)
            .GroupBy(v => v.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateName is not null)
        {
            throw new ArgumentException(
                $"'{key}' uses the name '{duplicateName.Key}' for more than one alias, so a lookup could not distinguish them.",
                nameof(aliases));
        }

        var undeclared = requiredAliases.Where(a => !aliases.ContainsKey(a)).ToList();
        if (undeclared.Count > 0)
        {
            throw new ArgumentException(
                $"'{key}' requires {string.Join(", ", undeclared)}, which it does not declare.",
                nameof(requiredAliases));
        }

        if (requiredAliases.Distinct().Count() != requiredAliases.Count)
        {
            throw new ArgumentException($"'{key}' lists the same required alias more than once.", nameof(requiredAliases));
        }

        RequiredCategories = ValidateRequiredCategories(key, requiredCategories ?? []);
        _categoryLabels = BuildCategoryLabels(key, categoryLabels ?? new Dictionary<StatusCategory, string>());

        _aliases = aliases.ToDictionary(a => a.Key, a => a.Value.Name.Trim());
        _aliasCategories = aliases.ToDictionary(a => a.Key, a => a.Value.Category);
        Moves = ValidateMoves(key, _aliases, requiredAliases, moves ?? []);
    }

    private static List<StatusCategory> ValidateRequiredCategories(string key, IReadOnlyCollection<StatusCategory> requiredCategories)
    {
        if (requiredCategories.Any(c => !Enum.IsDefined(c)))
        {
            throw new ArgumentException($"'{key}' requires a category that does not exist.", nameof(requiredCategories));
        }

        if (requiredCategories.Distinct().Count() != requiredCategories.Count)
        {
            throw new ArgumentException($"'{key}' lists the same required category more than once.", nameof(requiredCategories));
        }

        return [.. requiredCategories.Order()];
    }

    private static Dictionary<StatusCategory, string> BuildCategoryLabels(
        string key, IReadOnlyDictionary<StatusCategory, string> categoryLabels)
    {
        if (categoryLabels.Keys.Any(c => !Enum.IsDefined(c)))
        {
            throw new ArgumentException($"'{key}' labels a category that does not exist.", nameof(categoryLabels));
        }

        if (categoryLabels.Values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"'{key}' declares a category label with no text.", nameof(categoryLabels));
        }

        var labels = Enum.GetValues<StatusCategory>().ToDictionary(
            c => c,
            c => categoryLabels.TryGetValue(c, out var label) ? label.Trim() : c.ToString());

        var duplicate = labels.Values
            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"'{key}' uses the label '{duplicate.Key}' for more than one category, so a reader could not tell them apart.",
                nameof(categoryLabels));
        }

        return labels;
    }

    private static List<WorkflowMove> ValidateMoves(
        string key, IReadOnlyDictionary<int, string> aliases, IReadOnlyCollection<int> requiredAliases, IReadOnlyCollection<WorkflowMove> moves)
    {
        foreach (var move in moves)
        {
            Guard.Against.Null(move, nameof(moves));

            var ends = new[] { move.FromAlias, move.ToAlias }.OfType<int>().ToList();
            if (ends.Any(a => !aliases.ContainsKey(a)))
            {
                throw new ArgumentException($"'{key}' declares the move '{move.Action}' between aliases it does not declare.", nameof(moves));
            }

            // A required move names only required aliases, so a workflow that publishes always has both
            // ends: one to an optional alias could not be checked on a workflow that leaves the alias out.
            if (move.IsRequired && ends.Any(a => !requiredAliases.Contains(a)))
            {
                throw new ArgumentException($"'{key}' requires the move '{move.Action}', which names an alias it does not require.", nameof(moves));
            }
        }

        if (moves.Select(m => (m.FromAlias, m.ToAlias)).Distinct().Count() != moves.Count)
        {
            throw new ArgumentException($"'{key}' declares the same move more than once.", nameof(moves));
        }

        return [.. moves];
    }

    /// <summary>
    /// Stable identifier persisted on every workflow row. See the constructor for why it must never
    /// change once it has shipped.
    /// </summary>
    public string Key { get; }

    /// <summary>What to call this kind of record in a message or an admin screen.</summary>
    public string DisplayName { get; }

    /// <summary>
    /// The feature flag the owning module is released behind, or null when it is always available.
    /// </summary>
    /// <remarks>
    /// While the flag is off, workflow administration treats the owner type as absent: its workflows and
    /// assignments are left out of every list and read as not found, and none can be created, cloned or
    /// reassigned. The workflows are still seeded and still resolve, so switching the module on needs
    /// nothing else.
    /// </remarks>
    public string? FeatureFlag { get; }

    /// <summary>
    /// Every well-known meaning a status of this owner type can carry, value to name.
    /// </summary>
    public IReadOnlyDictionary<int, string> Aliases => _aliases;

    /// <summary>
    /// The aliases a workflow for this owner type must supply before it can be activated.
    /// </summary>
    /// <remarks>
    /// Enforced at activation rather than at first use: a workflow missing an alias its aggregates need
    /// would otherwise fail later, inside a domain method, on a record an administrator already created.
    /// </remarks>
    public IReadOnlyCollection<int> RequiredAliases { get; }

    /// <summary>
    /// The categories a workflow for this owner type must have at least one status in before it can be
    /// published, in category order.
    /// </summary>
    /// <remarks>
    /// A category carries most of the rules a status follows — whether a record is open, editable, or
    /// finished — so an owner type whose records must be able to get done declares Done here, and its
    /// workflows cannot leave it out. Declared per owner type rather than engine-wide because record
    /// types differ: one that needs only two steps declares two.
    /// </remarks>
    public IReadOnlyCollection<StatusCategory> RequiredCategories { get; }

    /// <summary>
    /// The category each alias belongs to, by alias value. A status carrying an alias must be in that
    /// category; see <see cref="WorkflowAlias"/>.
    /// </summary>
    public IReadOnlyDictionary<int, StatusCategory> AliasCategories => _aliasCategories;

    /// <summary>
    /// Whether a status in <paramref name="category"/> may carry <paramref name="alias"/>: always for
    /// <see cref="StatusWorkflow.NoAlias"/>, never for an alias this owner type does not declare, and
    /// otherwise only in the alias's own category.
    /// </summary>
    /// <remarks>
    /// An undeclared alias is refused because nothing resolves it, yet the alias lookup would still name
    /// it — a value retired from the vocabulary would go on labelling reports.
    /// </remarks>
    public bool Fits(int alias, StatusCategory category) =>
        alias == StatusWorkflow.NoAlias
        || (_aliasCategories.TryGetValue(alias, out var expected) && expected == category);

    /// <summary>
    /// Why a status in <paramref name="category"/> cannot carry <paramref name="alias"/>, or null when it can.
    /// </summary>
    public string? DescribeMisfit(int alias, StatusCategory category)
    {
        if (Fits(alias, category))
        {
            return null;
        }

        return _aliasCategories.TryGetValue(alias, out var expected)
            ? $"'{DescribeAlias(alias)}' belongs under {CategoryLabel(expected)}, not {CategoryLabel(category)}."
            : $"{DisplayName} statuses have no meaning numbered {alias}.";
    }

    /// <summary>
    /// Whether a workflow for this owner type may restrict which moves its records make.
    /// </summary>
    /// <remarks>
    /// Off by default because an aggregate that moves through named methods with their own domain rules
    /// (cut, release, roll back) could be contradicted by a custom transition set, unless it declares the
    /// <see cref="Moves"/> those methods cannot do without. A restricted workflow
    /// for an owner type without it is refused at publish, and its transitions are never enforced.
    /// </remarks>
    public bool SupportsRestrictedTransitions { get; }

    /// <summary>
    /// The moves this owner type's named methods make, or empty when its records move to any status they
    /// are given.
    /// </summary>
    /// <remarks>
    /// An owner type that declares moves changes status only through those methods, which resolve
    /// aliases, so its records leave only an aliased status or the initial one. For its workflows the
    /// declared moves replace the engine's general publish checks: a restricted workflow holds no other
    /// status (a reassignment could strand records in it), gives every unfinished status a declared
    /// move out, allows every required move, and lets a transition carry only the conditions its move
    /// accepts — none, for a transition no method makes.
    /// </remarks>
    public IReadOnlyList<WorkflowMove> Moves { get; }

    /// <summary>Whether this owner type declares its <see cref="Moves"/>.</summary>
    public bool DeclaresMoves => Moves.Count > 0;

    /// <summary>
    /// What this owner type calls each category, for every category: its declared label, or the enum's
    /// name when it declares none.
    /// </summary>
    /// <remarks>
    /// Words only. The category itself, and every rule and rollup keyed on it, stays the shared
    /// <see cref="StatusCategory"/>; what a record must reach to be in Done is still each workflow's to
    /// decide. No two categories share a label, case-insensitively.
    /// </remarks>
    public IReadOnlyDictionary<StatusCategory, string> CategoryLabels => _categoryLabels;

    /// <summary>What this owner type calls <paramref name="category"/>.</summary>
    public string CategoryLabel(StatusCategory category) =>
        _categoryLabels.TryGetValue(category, out var label) ? label : category.ToString();

    /// <summary>
    /// Names an alias, falling back to the raw value when it is not one this owner type declares.
    /// </summary>
    public string DescribeAlias(int alias) =>
        _aliases.TryGetValue(alias, out var name) ? name : alias.ToString();
}
