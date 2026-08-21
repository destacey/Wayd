namespace Wayd.Common.Application.Interfaces.ExternalWork;

/// <summary>
/// A person as referenced by an external delivery system on a work item. Connector-neutral: the
/// only field every system reliably supplies is <see cref="ExternalId"/>, so it is the key
/// identity mapping resolves on.
/// </summary>
/// <remarks>
/// Email is deliberately optional. Azure DevOps usually reports one, but Jira strips
/// <c>emailAddress</c> unless the app holds the profile scope, and most GitHub users hide theirs.
/// Matching on email alone therefore fails outright on two of the three systems this contract has
/// to serve, and fails on the third as soon as a person's address changes — which is what it did.
/// Email is a hint that seeds an automatic match; it is never the identity.
/// </remarks>
public interface IExternalUserRef
{
    /// <summary>
    /// The external system's stable, opaque identifier for this person — an Azure DevOps identity
    /// GUID, a Jira <c>accountId</c>, a GitHub user id. Survives email, display name, and domain
    /// changes, which is why mappings key on it.
    /// </summary>
    string ExternalId { get; }

    /// <summary>
    /// The work address the external system reports, when it reports one. Used only to seed an
    /// automatic match against an employee's known addresses.
    /// </summary>
    string? Email { get; }

    /// <summary>The person's display name, for the admin mapping UI.</summary>
    string? DisplayName { get; }

    /// <summary>
    /// The system's human-readable account handle (Azure DevOps <c>uniqueName</c>, GitHub
    /// <c>login</c>). Shown when there is no email, which for some connectors is the norm rather
    /// than the exception.
    /// </summary>
    string? Handle { get; }
}
