using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Tests.Sut.Identity;

public sealed class LastSeenWriterTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 1, 12, 0, 0);

    private static LastSeenWriter CreateWriter(int capacity = 1000) =>
        new(Mock.Of<IServiceScopeFactory>(), NullLogger<LastSeenWriter>.Instance, capacity);

    [Fact]
    public void RecordUserActivity_WithinInterval_QueuesOnlyTheFirst()
    {
        // Arrange
        var sut = CreateWriter();

        // Act
        var results = Enumerable.Range(0, 50)
            .Select(i => sut.RecordUserActivity("user-1", Now.Plus(Duration.FromSeconds(i))))
            .ToList();

        // Assert
        results.Count(queued => queued).Should().Be(1);
        results[0].Should().BeTrue();
    }

    [Fact]
    public void RecordUserActivity_OnceTheIntervalHasPassed_QueuesAgain()
    {
        // Arrange
        var sut = CreateWriter();
        sut.RecordUserActivity("user-1", Now);

        // Act
        var queued = sut.RecordUserActivity("user-1", Now.Plus(LastSeenWriter.UserActivityInterval));

        // Assert
        queued.Should().BeTrue();
    }

    [Fact]
    public void RecordTokenUse_WithinTheUserActivityIntervalButNotTheTokenInterval_IsAbsorbed()
    {
        // Arrange
        var sut = CreateWriter();
        var tokenId = Guid.NewGuid();
        sut.RecordTokenUse(tokenId, Now);

        // Act
        var queued = sut.RecordTokenUse(tokenId, Now.Plus(LastSeenWriter.UserActivityInterval));

        // Assert
        queued.Should().BeFalse();
    }

    [Fact]
    public void Record_ForDifferentSubjects_ThrottlesEachIndependently()
    {
        // Arrange
        var sut = CreateWriter();
        sut.RecordUserActivity("user-1", Now);

        // Act
        var otherUser = sut.RecordUserActivity("user-2", Now);
        var token = sut.RecordTokenUse(Guid.NewGuid(), Now);

        // Assert
        otherUser.Should().BeTrue();
        token.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenABatchFails_KeepsRunningAndReleasesTheBatch()
    {
        // Arrange
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Throws(new InvalidOperationException("Container unavailable"));
        using var sut = new LastSeenWriter(scopeFactory.Object, NullLogger<LastSeenWriter>.Instance);
        var ct = TestContext.Current.CancellationToken;

        // Act
        await sut.StartAsync(ct);
        sut.RecordUserActivity("user-1", Now);

        var released = false;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!released && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
            released = sut.RecordUserActivity("user-1", Now.Plus(Duration.FromSeconds(1)));
        }

        // Assert
        released.Should().BeTrue("a failed write must not hold the throttle");
        sut.ExecuteTask!.IsCompleted.Should().BeFalse("an escaping exception would stop the host");

        await sut.StopAsync(ct);
    }

    [Fact]
    public void Record_WhenTheQueueDropsASighting_ReleasesThatSubject()
    {
        // Arrange - with room for one, the second sighting drops the first.
        var sut = CreateWriter(capacity: 1);
        sut.RecordUserActivity("user-1", Now);
        sut.RecordUserActivity("user-2", Now);

        // Act
        var queued = sut.RecordUserActivity("user-1", Now.Plus(Duration.FromSeconds(1)));

        // Assert
        queued.Should().BeTrue("the dropped sighting was never written, so it must not hold the throttle");
    }
}
