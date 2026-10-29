using CSharpFunctionalExtensions;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.Integrations.MicrosoftGraph;

/// <inheritdoc />
public sealed class TeamsDelegatedChatMessaging : ITeamsDelegatedChatMessaging
{
    private static readonly string[] GraphChatMessageSendScope = ["https://graph.microsoft.com/ChatMessage.Send"];

    /// <summary>
    /// Scopes required when exchanging a user token to post a chat message.
    /// </summary>
    public static IReadOnlyCollection<string> ChatMessageSendScopes => GraphChatMessageSendScope;

    /// <inheritdoc />
    public async Task<Result> SendTextMessageAsync(
        string chatId,
        string messageBody,
        string graphAccessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chatId))
            return Result.Failure("A chat id is required to send a Teams message.");

        var client = BuildUserClient(graphAccessToken);
        var message = new ChatMessage
        {
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = messageBody,
            },
        };

        try
        {
            await client.Chats[chatId].Messages.PostAsync(message, cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure($"Microsoft Graph rejected the Teams message: {ex.Message}");
        }
    }

    private static GraphServiceClient BuildUserClient(string accessToken)
    {
        var provider = new BaseBearerTokenAuthenticationProvider(new StaticAccessTokenProvider(accessToken));
        return new GraphServiceClient(provider);
    }

    private sealed class StaticAccessTokenProvider(string accessToken) : IAccessTokenProvider
    {
        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(accessToken);
    }
}
