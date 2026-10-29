using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.Integrations.MicrosoftGraph;

/// <inheritdoc />
public sealed class TeamsUserChatMessageSender(
    ITeamsGraphUserTokenExchange teamsGraphUserTokenExchange,
    ITeamsDelegatedChatMessaging teamsDelegatedChatMessaging) : ITeamsUserChatMessageSender
{
    private readonly ITeamsGraphUserTokenExchange _teamsGraphUserTokenExchange = teamsGraphUserTokenExchange;
    private readonly ITeamsDelegatedChatMessaging _teamsDelegatedChatMessaging = teamsDelegatedChatMessaging;

    /// <inheritdoc />
    public async Task<Result> SendAsSignedInUserAsync(
        TeamsGraphCredentials connectionCredentials,
        string chatId,
        string messageBody,
        string userAssertion,
        CancellationToken cancellationToken)
    {
        var graphToken = await _teamsGraphUserTokenExchange.ExchangeForGraphAccessTokenAsync(
            connectionCredentials,
            userAssertion,
            TeamsDelegatedChatMessaging.ChatMessageSendScopes,
            cancellationToken);
        if (graphToken.IsFailure)
            return Result.Failure(graphToken.Error);

        return await _teamsDelegatedChatMessaging.SendTextMessageAsync(
            chatId,
            messageBody,
            graphToken.Value,
            cancellationToken);
    }
}
