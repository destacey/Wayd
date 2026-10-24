using Ardalis.GuardClauses;

namespace Wayd.Common.Domain.Following;

/// <summary>
/// What a module declares about one kind of record people can follow: the key its follows are stored
/// under and what to call it.
/// </summary>
/// <remarks>
/// The seam that keeps following free of any module's vocabulary, as <c>WorkflowOwnerDescriptor</c> is for
/// the workflow engine. A module declares its descriptors as static readonly fields in its own domain
/// project and registers them with <see cref="FollowOwners"/>, so a new followable record is a new file in
/// that module and no change here.
/// <para>
/// Not a record: <see cref="FollowOwners"/> treats re-registering the same instance as harmless and a
/// second instance under one key as a conflict, which value equality would blur.
/// </para>
/// </remarks>
public sealed class FollowOwnerDescriptor
{
    /// <summary>The longest key the <c>Follows.OwnerType</c> column holds.</summary>
    public const int KeyMaxLength = 64;

    /// <param name="key">
    /// Stable identifier, persisted on every follow row. Namespaced by module — <c>ppm.project</c> — so two
    /// modules cannot collide on a common word, and spelled the same as the record's workflow owner type
    /// when it has one. <strong>Never change one after it ships</strong>: renaming it orphans every follow
    /// that carries it.
    /// </param>
    /// <param name="displayName">What to call this kind of record in a list heading or a message.</param>
    public FollowOwnerDescriptor(string key, string displayName)
    {
        Key = Guard.Against.NullOrWhiteSpace(key, nameof(key)).Trim();
        DisplayName = Guard.Against.NullOrWhiteSpace(displayName, nameof(displayName)).Trim();

        if (Key.Length > KeyMaxLength)
        {
            throw new ArgumentException($"'{Key}' is longer than {KeyMaxLength} characters.", nameof(key));
        }
    }

    /// <summary>
    /// Stable identifier persisted on every follow row. See the constructor for why it must never change
    /// once it has shipped.
    /// </summary>
    public string Key { get; }

    /// <summary>What to call this kind of record in a list heading or a message.</summary>
    public string DisplayName { get; }
}
