namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// Outbound Microsoft Graph calls for Teams communication connections. Separate from
/// <see cref="Interfaces.ExternalPeople.IEntraEmployeeSource"/> — people sync stays read-only.
/// </summary>
public interface ITeamsCommunicationGraphClient
{
    /// <summary>
    /// Proves the connection's application permissions: looks up the recipient, creates a Teams
    /// chat, and creates a calendar event. Does not post a chat message.
    /// </summary>
    Task<Result> RunHelloWorldTestAsync(
        TeamsGraphCredentials credentials,
        string recipientEmail,
        CancellationToken cancellationToken);

    /// <summary>
    /// The user's Graph id when <paramref name="email"/> matches <c>mail</c> or <c>userPrincipalName</c>.
    /// A success with no id means the tenant has no such user. A failure means Graph refused the lookup.
    /// </summary>
    Task<Result<string?>> FindUserIdByEmailAsync(
        TeamsGraphCredentials credentials,
        string email,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a group chat named <paramref name="topic"/> whose members are all owners.
    /// Does not post a message. Fails when Graph rejects the chat or returns no id.
    /// </summary>
    Task<Result<TeamsCreatedChat>> CreateGroupChatAsync(
        TeamsGraphCredentials credentials,
        string topic,
        IReadOnlyList<string> memberUserIds,
        CancellationToken cancellationToken);
}
