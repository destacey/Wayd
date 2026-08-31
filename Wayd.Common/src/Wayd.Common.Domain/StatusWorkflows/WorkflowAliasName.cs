using Ardalis.GuardClauses;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// The name of one well-known alias, so that a query joining on an alias column reads as
/// <c>Succeeded</c> rather than <c>21</c>.
/// </summary>
/// <remarks>
/// Alias columns are <c>int</c> because their meaning belongs to the owning module rather than the
/// engine — an <c>EnumConverter</c> here would put a module's enum in Infrastructure, and there is no
/// converter that could serve every module. This table restores readability without giving that up:
/// the hot columns stay 4-byte integers and the names are data, rebuilt from each registered
/// <see cref="WorkflowOwnerDescriptor"/> at startup, so a new alias needs no migration.
/// <para>
/// Reference data only. Nothing in the domain reads it — resolution goes through the descriptor — and
/// no foreign key points at it, so a row going missing degrades a query rather than breaking a record.
/// </para>
/// </remarks>
public sealed class WorkflowAliasName
{
    private WorkflowAliasName() { }

    public WorkflowAliasName(string ownerType, int alias, string name)
    {
        OwnerType = Guard.Against.NullOrWhiteSpace(ownerType, nameof(ownerType)).Trim();
        Name = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();

        if (alias == StatusWorkflow.NoAlias)
        {
            throw new ArgumentException("NoAlias carries no meaning and has no name.", nameof(alias));
        }

        Alias = alias;
    }

    /// <summary>The owner type whose vocabulary this alias belongs to.</summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>The stored alias value.</summary>
    public int Alias { get; private init; }

    /// <summary>What that value means.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Updates the name after a module renames one of its aliases in code.
    /// </summary>
    public void Rename(string name) => Name = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
}
