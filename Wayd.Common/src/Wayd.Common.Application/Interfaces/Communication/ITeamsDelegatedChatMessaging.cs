namespace Wayd.Common.Application.Interfaces.Communication;

/// <summary>
/// Sends Teams chat messages with a delegated Graph token (signed-in user). Used by connection
/// verification and, later, incident communication approval.
/// </summary>
public interface ITeamsDelegatedChatMessaging
{
    /// <summary>
    /// Posts a text message to <paramref name="chatId"/> using <paramref name="graphAccessToken"/>
    /// acquired for the signed-in user.
    /// </summary>
    Task<Result> SendTextMessageAsync(
        string chatId,
        string messageBody,
        string graphAccessToken,
        CancellationToken cancellationToken);
}
