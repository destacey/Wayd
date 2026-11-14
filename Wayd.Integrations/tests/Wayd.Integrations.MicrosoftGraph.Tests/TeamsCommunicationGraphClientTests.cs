using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions.Store;
using Microsoft.Kiota.Serialization.Json;
using Wayd.Common.Application.Interfaces.Communication;
using Wayd.Tests.Shared;
using Xunit;

namespace Wayd.Integrations.MicrosoftGraph.Tests;

public sealed class TeamsCommunicationGraphClientTests
{
    private const string Recipient = "admin@contoso.com";

    private static readonly TestingDateTimeProvider _clock =
        new(new DateTime(2026, 3, 15, 18, 30, 0, DateTimeKind.Utc));

    [Fact]
    public async Task RunHelloWorldTest_WhenGraphRejectsTheMailFilter_LooksUpByUserPrincipalName()
    {
        // Arrange
        var adapter = new RecordingGraphAdapter { RejectMailFilter = true };
        var sut = CreateClient(adapter);

        // Act
        var result = await sut.RunHelloWorldTestAsync(
            new TeamsGraphCredentials("tenant", "client", "secret"),
            Recipient,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        adapter.Filters.Should().Equal(
            $"mail eq '{Recipient}'",
            $"userPrincipalName eq '{Recipient}'");
        adapter.Filters.Should().NotContain(filter => filter.Contains(" or ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunHelloWorldTest_SendsAllDayEventAtUtcMidnight()
    {
        // Arrange
        var adapter = new RecordingGraphAdapter();
        var sut = CreateClient(adapter);

        // Act
        var result = await sut.RunHelloWorldTestAsync(
            new TeamsGraphCredentials("tenant", "client", "secret"),
            Recipient,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var body = adapter.CalendarBodies.Should().ContainSingle().Subject;
        var dateTimes = Regex.Matches(body, "\"dateTime\"\\s*:\\s*\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();
        dateTimes.Should().BeEquivalentTo("2026-03-15T00:00:00", "2026-03-16T00:00:00");
        dateTimes.Should().OnlyContain(value =>
            Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T00:00:00$"));
        body.Should().NotContain("18:30:00");
    }

    [Fact]
    public async Task CreateGroupChat_PostsOwnersAndUsesTheGraphLink()
    {
        // Arrange
        var adapter = new RecordingGraphAdapter
        {
            ChatWebUrl = "https://teams.microsoft.com/l/chat/chat-1/0",
        };
        var sut = CreateClient(adapter);

        // Act
        var result = await sut.CreateGroupChatAsync(
            new TeamsGraphCredentials("tenant", "client", "secret"),
            " INC-42 Checkout unavailable ",
            ["user-a", "user-b", "user-a"],
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Id.Should().Be("chat-1");
        result.Value.Link.Should().Be("https://teams.microsoft.com/l/chat/chat-1/0");
        var body = adapter.ChatBodies.Should().ContainSingle().Subject;
        body.Should().Contain("INC-42 Checkout unavailable");
        body.Should().Contain("user-a");
        body.Should().Contain("user-b");
        body.Should().Contain("owner");
    }

    [Fact]
    public async Task CreateGroupChat_UsesATeamsDeepLink_WhenGraphReturnsNoHttpsLink()
    {
        // Arrange
        var adapter = new RecordingGraphAdapter { ChatId = "19:abc@thread.v2" };
        var sut = CreateClient(adapter);

        // Act
        var result = await sut.CreateGroupChatAsync(
            new TeamsGraphCredentials("tenant", "client", "secret"),
            "INC-1 Outage",
            ["user-a"],
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Link.Should().Be(
            "https://teams.microsoft.com/l/chat/19%3Aabc%40thread.v2/0");
    }

    [Fact]
    public async Task CreateGroupChat_ReportsTheGraphRejection()
    {
        // Arrange
        var adapter = new RecordingGraphAdapter { ThrowOnChatPost = true };
        var sut = CreateClient(adapter);

        // Act
        var result = await sut.CreateGroupChatAsync(
            new TeamsGraphCredentials("tenant", "client", "secret"),
            "INC-1 Outage",
            ["user-a"],
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("could not create the Teams chat");
    }

    [Fact]
    public async Task CreateGroupChat_DoesNotCallGraph_WhenNobodyCanBeAdded()
    {
        // Arrange
        var adapter = new RecordingGraphAdapter();
        var sut = CreateClient(adapter);

        // Act
        var result = await sut.CreateGroupChatAsync(
            new TeamsGraphCredentials("tenant", "client", "secret"),
            "INC-1 Outage",
            [],
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        adapter.ChatBodies.Should().BeEmpty();
    }

    [Fact]
    public async Task FindUserIdByEmail_ReturnsNull_WhenTheTenantHasNoMatch()
    {
        // Arrange
        var adapter = new RecordingGraphAdapter { ReturnNoUser = true };
        var sut = CreateClient(adapter);

        // Act
        var result = await sut.FindUserIdByEmailAsync(
            new TeamsGraphCredentials("tenant", "client", "secret"),
            Recipient,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeNull();
    }

    private static TeamsCommunicationGraphClient CreateClient(RecordingGraphAdapter adapter) =>
        new(
            NullLogger<TeamsCommunicationGraphClient>.Instance,
            _clock,
            _ => new GraphServiceClient(adapter, "https://graph.microsoft.com/v1.0"));

    private sealed class RecordingGraphAdapter : IRequestAdapter
    {
        public bool RejectMailFilter { get; init; }

        public bool ReturnNoUser { get; init; }

        public bool ThrowOnChatPost { get; init; }

        public string ChatId { get; init; } = "chat-1";

        public string? ChatWebUrl { get; init; }

        public List<string> Filters { get; } = [];

        public List<string> CalendarBodies { get; } = [];

        public List<string> ChatBodies { get; } = [];

        public ISerializationWriterFactory SerializationWriterFactory { get; } =
            new JsonSerializationWriterFactory();

        public string? BaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";

        public void EnableBackingStore(IBackingStoreFactory backingStoreFactory)
        {
        }

        public Task<ModelType?> SendAsync<ModelType>(
            RequestInformation requestInfo,
            ParsableFactory<ModelType> factory,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default)
            where ModelType : IParsable
        {
            if (requestInfo.UrlTemplate?.Contains("/chats", StringComparison.Ordinal) == true)
            {
                ChatBodies.Add(ReadContent(requestInfo));
                if (ThrowOnChatPost)
                    throw new InvalidOperationException("Graph rejected the chat.");

                return Task.FromResult((ModelType?)(object)new Chat { Id = ChatId, WebUrl = ChatWebUrl });
            }

            if (requestInfo.UrlTemplate?.Contains("calendar/events", StringComparison.Ordinal) == true)
            {
                CalendarBodies.Add(ReadContent(requestInfo));
                return Task.FromResult((ModelType?)(object)new Event { Id = "event-1" });
            }

            var filter = ReadFilter(requestInfo)
                ?? throw new InvalidOperationException(
                    $"Graph request had no user filter. Template: {requestInfo.UrlTemplate}. Query: {DumpQuery(requestInfo)}");
            Filters.Add(filter);

            if (filter.Contains(" or ", StringComparison.OrdinalIgnoreCase)
                || (RejectMailFilter && filter.StartsWith("mail eq ", StringComparison.Ordinal)))
            {
                throw new ODataError
                {
                    Error = new MainError
                    {
                        Code = "Request_UnsupportedQuery",
                        Message = "Unsupported or invalid query filter clause specified for property 'mail' of resource 'User'.",
                    },
                };
            }

            if (ReturnNoUser)
                return Task.FromResult((ModelType?)(object)new UserCollectionResponse { Value = [] });

            var user = new User
            {
                Id = "user-1",
                Mail = Recipient,
                UserPrincipalName = Recipient,
            };
            return Task.FromResult((ModelType?)(object)new UserCollectionResponse { Value = [user] });
        }

        public Task<IEnumerable<ModelType>?> SendCollectionAsync<ModelType>(
            RequestInformation requestInfo,
            ParsableFactory<ModelType> factory,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default)
            where ModelType : IParsable =>
            throw new NotSupportedException();

        public Task<ModelType?> SendPrimitiveAsync<ModelType>(
            RequestInformation requestInfo,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IEnumerable<ModelType>?> SendPrimitiveCollectionAsync<ModelType>(
            RequestInformation requestInfo,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SendNoContentAsync(
            RequestInformation requestInfo,
            Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<T?> ConvertToNativeRequestAsync<T>(
            RequestInformation requestInfo,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static string? ReadFilter(RequestInformation request)
        {
            foreach (var pair in request.QueryParameters)
            {
                if (pair.Value is string text && text.Contains(" eq ", StringComparison.Ordinal))
                    return text;

                var printed = pair.Value?.ToString();
                if (printed is not null && printed.Contains(" eq ", StringComparison.Ordinal))
                    return printed;
            }

            return null;
        }

        private static string DumpQuery(RequestInformation request) =>
            string.Join(", ", request.QueryParameters.Select(pair => $"{pair.Key}={pair.Value}"));

        private static string ReadContent(RequestInformation request)
        {
            if (request.Content is null)
                return string.Empty;

            if (request.Content.CanSeek)
                request.Content.Position = 0;

            using var reader = new StreamReader(request.Content, leaveOpen: true);
            return reader.ReadToEnd();
        }
    }
}
