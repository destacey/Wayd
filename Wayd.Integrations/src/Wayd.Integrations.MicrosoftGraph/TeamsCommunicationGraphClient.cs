using Azure.Identity;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.Integrations.MicrosoftGraph;

/// <summary>
/// Graph client for Teams communication connections. Builds a per-connection
/// <see cref="GraphServiceClient"/>; does not share state with people sync.
/// </summary>
public sealed class TeamsCommunicationGraphClient(
    ITeamsUserChatMessageSender teamsUserChatMessageSender,
    ILogger<TeamsCommunicationGraphClient> logger)
    : ITeamsCommunicationGraphClient
{
    private const string HelloWorldChatTopic = "Wayd connection test";
    private const string HelloWorldChatBody =
        "This Teams message confirms your Wayd Microsoft Teams connection can reach you in this tenant.";
    private const string HelloWorldEventSubject = "Wayd connection test";
    private const string HelloWorldEventBody =
        "This all-day event confirms your Wayd Microsoft Teams connection can write to your calendar.";

    private readonly ITeamsUserChatMessageSender _teamsUserChatMessageSender = teamsUserChatMessageSender;
    private readonly ILogger<TeamsCommunicationGraphClient> _logger = logger;

    /// <inheritdoc />
    public async Task<Result> RunHelloWorldTestAsync(
        TeamsGraphCredentials credentials,
        string recipientEmail,
        string userAssertion,
        CancellationToken cancellationToken)
    {
        try
        {
            var appClient = BuildAppClient(credentials);
            var user = await FindUserByEmailAsync(appClient, recipientEmail, cancellationToken);
            if (user?.Id is null)
            {
                return Result.Failure(
                    $"No user with email '{recipientEmail}' was found in the Teams connection's tenant.");
            }

            var chatId = await CreateHelloWorldChatAsync(appClient, user.Id, cancellationToken);

            var send = await _teamsUserChatMessageSender.SendAsSignedInUserAsync(
                credentials,
                chatId,
                HelloWorldChatBody,
                userAssertion,
                cancellationToken);
            if (send.IsFailure)
                return send;

            await CreateHelloWorldCalendarEventAsync(appClient, user.Id, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Teams hello-world Graph test failed for tenant {TenantId}", credentials.TenantId);
            return Result.Failure($"Microsoft Graph rejected the connection test: {ex.Message}");
        }
    }

    private static GraphServiceClient BuildAppClient(TeamsGraphCredentials credentials)
    {
        var credential = new ClientSecretCredential(
            credentials.TenantId,
            credentials.ClientId,
            credentials.ClientSecret);
        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    private static async Task<User?> FindUserByEmailAsync(
        GraphServiceClient client,
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        var escaped = recipientEmail.Replace("'", "''", StringComparison.Ordinal);
        var filter = $"mail eq '{escaped}' or userPrincipalName eq '{escaped}'";

        var response = await client.Users.GetAsync(
            requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = filter;
                requestConfiguration.QueryParameters.Select = ["id", "mail", "userPrincipalName"];
                requestConfiguration.QueryParameters.Top = 1;
            },
            cancellationToken);

        return response?.Value?.FirstOrDefault();
    }

    private static async Task<string> CreateHelloWorldChatAsync(
        GraphServiceClient client,
        string userId,
        CancellationToken cancellationToken)
    {
        var chat = new Chat
        {
            ChatType = ChatType.Group,
            Topic = HelloWorldChatTopic,
            Members =
            [
                new AadUserConversationMember
                {
                    OdataType = "#microsoft.graph.aadUserConversationMember",
                    Roles = ["owner"],
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users('{userId}')",
                    },
                },
            ],
        };

        var createdChat = await client.Chats.PostAsync(chat, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Graph did not return a chat id for the hello-world test.");

        if (string.IsNullOrEmpty(createdChat.Id))
            throw new InvalidOperationException("Graph returned a chat without an id for the hello-world test.");

        return createdChat.Id;
    }

    private static async Task CreateHelloWorldCalendarEventAsync(
        GraphServiceClient client,
        string userId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var end = today.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var calendarEvent = new Event
        {
            Subject = HelloWorldEventSubject,
            IsAllDay = true,
            Start = new DateTimeTimeZone { DateTime = start, TimeZone = "UTC" },
            End = new DateTimeTimeZone { DateTime = end, TimeZone = "UTC" },
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = HelloWorldEventBody,
            },
        };

        await client.Users[userId].Calendar.Events.PostAsync(calendarEvent, cancellationToken: cancellationToken);
    }
}
