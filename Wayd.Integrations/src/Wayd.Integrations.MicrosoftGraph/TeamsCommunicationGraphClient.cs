using System.Globalization;
using Azure.Identity;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.Integrations.MicrosoftGraph;

/// <summary>
/// Graph client for Teams communication connections. Builds a per-connection
/// <see cref="GraphServiceClient"/>; does not share state with people sync.
/// </summary>
public sealed class TeamsCommunicationGraphClient : ITeamsCommunicationGraphClient
{
    private const string HelloWorldChatTopic = "Wayd connection test";
    private const string HelloWorldEventSubject = "Wayd connection test";
    private const string HelloWorldEventBody =
        "This all-day event confirms your Wayd Microsoft Teams connection can write to your calendar.";

    /// <summary>
    /// Identifiers tried one at a time. Graph rejects
    /// <c>mail eq … or userPrincipalName eq …</c> with <c>Request_UnsupportedQuery</c>,
    /// and that rejection must not fail the whole hello-world test.
    /// </summary>
    private static readonly string[] _userLookupProperties = ["mail", "userPrincipalName"];

    private readonly ILogger<TeamsCommunicationGraphClient> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly Func<TeamsGraphCredentials, GraphServiceClient> _clientFactory;

    public TeamsCommunicationGraphClient(
        ILogger<TeamsCommunicationGraphClient> logger,
        IDateTimeProvider dateTimeProvider)
        : this(logger, dateTimeProvider, BuildAppClient)
    {
    }

    internal TeamsCommunicationGraphClient(
        ILogger<TeamsCommunicationGraphClient> logger,
        IDateTimeProvider dateTimeProvider,
        Func<TeamsGraphCredentials, GraphServiceClient> clientFactory)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _clientFactory = clientFactory;
    }

    /// <inheritdoc />
    public async Task<Result> RunHelloWorldTestAsync(
        TeamsGraphCredentials credentials,
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        try
        {
            var appClient = _clientFactory(credentials);
            var user = await FindUserByEmailAsync(appClient, recipientEmail, cancellationToken);
            if (user?.Id is null)
            {
                return Result.Failure(
                    $"No user with email '{recipientEmail}' was found in the Teams connection's tenant.");
            }

            await CreateHelloWorldChatAsync(appClient, user.Id, cancellationToken);
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

    private async Task<User?> FindUserByEmailAsync(
        GraphServiceClient client,
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        var escaped = recipientEmail.Replace("'", "''", StringComparison.Ordinal);
        ODataError? rejectedFilter = null;
        var anyFilterAccepted = false;

        foreach (var property in _userLookupProperties)
        {
            try
            {
                var user = await QueryUserAsync(client, $"{property} eq '{escaped}'", cancellationToken);
                anyFilterAccepted = true;
                if (user?.Id is not null)
                    return user;
            }
            catch (ODataError ex) when (IsUnsupportedUserFilter(ex))
            {
                rejectedFilter = ex;
                _logger.LogWarning(
                    ex,
                    "Microsoft Graph rejected the {Property} user lookup filter; trying another identifier.",
                    property);
            }
        }

        if (!anyFilterAccepted && rejectedFilter is not null)
            throw rejectedFilter;

        return null;
    }

    private static async Task<User?> QueryUserAsync(
        GraphServiceClient client,
        string filter,
        CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// True when Graph refused the filter itself. An authorization or throttling error is not a
    /// filter rejection and must still fail the hello-world test.
    /// </summary>
    private static bool IsUnsupportedUserFilter(ODataError error)
    {
        if (string.Equals(error.Error?.Code, "Request_UnsupportedQuery", StringComparison.OrdinalIgnoreCase))
            return true;

        var message = error.Error?.Message ?? error.Message;
        return message.Contains("Unsupported", StringComparison.OrdinalIgnoreCase)
            && message.Contains("filter", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CreateHelloWorldChatAsync(
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
    }

    private async Task CreateHelloWorldCalendarEventAsync(
        GraphServiceClient client,
        string userId,
        CancellationToken cancellationToken)
    {
        // Graph rejects a date-only value for an all-day event. The day starts at midnight and the
        // end is the next midnight; the time zone is carried separately, so the timestamp has no offset.
        var startDate = _dateTimeProvider.Today;
        var calendarEvent = new Event
        {
            Subject = HelloWorldEventSubject,
            IsAllDay = true,
            Start = new DateTimeTimeZone
            {
                DateTime = GraphMidnight(startDate),
                TimeZone = "UTC",
            },
            End = new DateTimeTimeZone
            {
                DateTime = GraphMidnight(startDate.PlusDays(1)),
                TimeZone = "UTC",
            },
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = HelloWorldEventBody,
            },
        };

        await client.Users[userId].Calendar.Events.PostAsync(calendarEvent, cancellationToken: cancellationToken);
    }

    // Graph reads this timestamp as text. Composite formatting follows the current culture,
    // which can rewrite the digits Graph expects.
    private static string GraphMidnight(LocalDate date) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{date.Year:0000}-{date.Month:00}-{date.Day:00}T00:00:00");
}
