using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Web.Api.Controllers.Admin;
using Wayd.Web.Api.Models.Admin.Messaging;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wolverine;
using Wolverine.Persistence.Durability;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// MessagingController against the real Wolverine SQL Server message store: counts, dead-letter
/// query/detail, replay, and discard. Dead letters are seeded through Wolverine's own
/// <see cref="IMessageInbox.MoveToDeadLetterStorageAsync"/> (the same path a real handler failure takes
/// after the retry policy is exhausted) rather than raw SQL, so the tests do not pin the envelope table
/// schema. The seeded envelopes use a fictional message type with no handler, so a replayed envelope
/// that the durability agent picks up cannot trigger real application handlers.
/// </summary>
[Trait("Category", "Docker")]
public sealed class MessagingControllerTests(WaydSqlServerApiFactory factory)
    : IClassFixture<WaydSqlServerApiFactory>
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task GetCounts_ReturnsPersistedCountsSnapshot()
    {
        // Arrange — boot the real host so the wolverine schema is provisioned.
        _ = _factory.CreateClient();
        var controller = CreateController();

        // Act
        var response = await controller.GetCounts();

        // Assert
        var counts = Assert.IsType<MessagingCountsResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.True(counts.Incoming >= 0);
        Assert.True(counts.DeadLetter >= 0);
    }

    [Fact]
    public async Task GetDeadLetters_ReturnsSeededEnvelope_AndDetailExposesBody()
    {
        // Arrange
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var controller = CreateController();
        var envelopeId = await SeedDeadLetter(ct);

        // Act
        var listResponse = await controller.GetDeadLetters(ct, pageSize: 500);
        var detailResponse = await controller.GetDeadLetterById(envelopeId);

        // Assert — the seeded envelope shows up in the page with its failure metadata...
        var page = Assert.IsType<DeadLetterMessagesResponse>(Assert.IsType<OkObjectResult>(listResponse.Result).Value);
        Assert.True(page.TotalCount >= 1);
        var summary = Assert.Single(page.Items, i => i.Id == envelopeId);
        Assert.Equal(FakeMessageType, summary.MessageType);
        Assert.Equal(typeof(InvalidOperationException).FullName, summary.ExceptionType);
        Assert.Equal("Simulated handler failure", summary.ExceptionMessage);

        // ...and the detail endpoint surfaces the serialized JSON body.
        var detail = Assert.IsType<DeadLetterMessageDetailsResponse>(Assert.IsType<OkObjectResult>(detailResponse.Result).Value);
        Assert.Equal(envelopeId, detail.Id);
        Assert.NotNull(detail.Body);
        Assert.Contains("\"marker\"", detail.Body);
    }

    [Fact]
    public async Task GetDeadLetterById_UnknownId_ReturnsNotFound()
    {
        // Arrange
        _ = _factory.CreateClient();
        var controller = CreateController();

        // Act
        var response = await controller.GetDeadLetterById(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task ReplayDeadLetters_MarksEnvelopeReplayable()
    {
        // Arrange
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var controller = CreateController();
        var envelopeId = await SeedDeadLetter(ct);

        // Act
        var response = await controller.ReplayDeadLetters(
            new DeadLetterMessagesRequest { Ids = [envelopeId] }, ct);

        // Assert — accepted, and the envelope is now marked replayable OR already swept back to the
        // incoming queue by the durability agent (a race we tolerate rather than flake on: both states
        // are the successful outcome of a replay).
        Assert.IsType<AcceptedResult>(response);
        var store = _factory.Services.GetRequiredService<IMessageStore>();
        var envelope = await store.DeadLetters.DeadLetterEnvelopeByIdAsync(envelopeId, null);
        Assert.True(envelope is null || envelope.Replayable);
    }

    [Fact]
    public async Task DiscardDeadLetters_DeletesEnvelope()
    {
        // Arrange
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var controller = CreateController();
        var envelopeId = await SeedDeadLetter(ct);

        // Act
        var response = await controller.DiscardDeadLetters(
            new DeadLetterMessagesRequest { Ids = [envelopeId] }, ct);

        // Assert — gone from the store entirely.
        Assert.IsType<NoContentResult>(response);
        var detailResponse = await controller.GetDeadLetterById(envelopeId);
        Assert.IsType<NotFoundResult>(detailResponse.Result);
    }

    [Fact]
    public async Task ReplayAndDiscard_WithNullOrEmptyIds_ReturnBadRequest()
    {
        // Arrange — the Ids property initializer does not survive an explicit {"ids": null} payload,
        // so both action guards must handle null as well as empty.
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var controller = CreateController();

        // Act
        var replayNull = await controller.ReplayDeadLetters(new DeadLetterMessagesRequest { Ids = null! }, ct);
        var replayEmpty = await controller.ReplayDeadLetters(new DeadLetterMessagesRequest(), ct);
        var discardNull = await controller.DiscardDeadLetters(new DeadLetterMessagesRequest { Ids = null! }, ct);
        var discardEmpty = await controller.DiscardDeadLetters(new DeadLetterMessagesRequest(), ct);

        // Assert
        Assert.IsType<BadRequestObjectResult>(replayNull);
        Assert.IsType<BadRequestObjectResult>(replayEmpty);
        Assert.IsType<BadRequestObjectResult>(discardNull);
        Assert.IsType<BadRequestObjectResult>(discardEmpty);
    }

    /// <summary>
    /// A fictional, handler-less message type. Wolverine stores the dead letter regardless of whether
    /// the type resolves, and a replay of it can never dispatch into real application code.
    /// </summary>
    private const string FakeMessageType = "Wayd.Web.Api.IntegrationTests.FakeDeadLetterMessage";

    private MessagingController CreateController()
    {
        var store = _factory.Services.GetRequiredService<IMessageStore>();
        return new MessagingController(store)
        {
            // ProblemDetailsExtensions.ForBadRequest reads the request/trace info off HttpContext.
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private async Task<Guid> SeedDeadLetter(CancellationToken ct)
    {
        _ = ct; // MoveToDeadLetterStorageAsync has no cancellable overload.

        var store = _factory.Services.GetRequiredService<IMessageStore>();

        var envelope = new Envelope
        {
            Id = Guid.NewGuid(),
            MessageType = FakeMessageType,
            Data = JsonSerializer.SerializeToUtf8Bytes(new { marker = "dead-letter-integration-test" }),
            ContentType = "application/json",
            Destination = new Uri("local://durable/"),
            SentAt = DateTimeOffset.UtcNow,
        };

        await store.Inbox.MoveToDeadLetterStorageAsync(
            envelope, new InvalidOperationException("Simulated handler failure"));

        return envelope.Id;
    }
}
