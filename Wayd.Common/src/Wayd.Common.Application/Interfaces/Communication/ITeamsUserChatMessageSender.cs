namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// Sends a Teams chat message as the signed-in user by exchanging their Entra access token for
/// Graph and calling <c>ChatMessage.Send</c>.
/// </summary>
public interface ITeamsUserChatMessageSender
{
    /// <summary>
    /// Posts <paramref name="messageBody"/> to <paramref name="chatId"/> on behalf of the user
    /// identified by <paramref name="userAssertion"/>.
    /// </summary>
    Task<Result> SendAsSignedInUserAsync(
        TeamsGraphCredentials connectionCredentials,
        string chatId,
        string messageBody,
        string userAssertion,
        CancellationToken cancellationToken);
}
