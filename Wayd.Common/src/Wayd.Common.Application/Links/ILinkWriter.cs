namespace Wayd.Common.Application.Links;

/// <summary>
/// Changes a record's links: the one path every change takes, whoever asks for it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>It checks no caller.</strong> A link command asks the record's owner type whether the caller may manage its
/// links before calling this; an import and a module deleting its own record call it with no one to ask. So every
/// change is validated, recorded and told to the owner type the same way.
/// </para>
/// <para>
/// <strong>It never saves.</strong> The changes are tracked on the shared context, so the caller's save commits them
/// with whatever else the request changed — a deleted project and its links, in one transaction.
/// </para>
/// </remarks>
public interface ILinkWriter
{
    /// <summary>
    /// Adds a link to a record and returns its id. Fails when the owner type is not registered or the address is not
    /// a web address. A record may hold more than one link to the same address.
    /// </summary>
    Task<Result<Guid>> Add(string ownerType, Guid ownerId, string name, string url, EventActor actor, CancellationToken cancellationToken);

    /// <summary>
    /// Changes a link's name and address. Fails as <see cref="Add"/> does, and when the record has no such link.
    /// Saving what the link already says changes nothing.
    /// </summary>
    Task<Result> Update(string ownerType, Guid ownerId, Guid linkId, string name, string url, EventActor actor, CancellationToken cancellationToken);

    /// <summary>Removes one of a record's links. Fails when the record has no such link.</summary>
    Task<Result> Remove(string ownerType, Guid ownerId, Guid linkId, EventActor actor, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every link a record has, for a module deleting the record: a link has no foreign key to its owner, so
    /// it would otherwise outlive it.
    /// </summary>
    /// <exception cref="ArgumentException">The owner type is not registered.</exception>
    Task RemoveAll(string ownerType, Guid ownerId, EventActor actor, CancellationToken cancellationToken);
}
