using Wayd.Common.Application.Dispatching;
using Wayd.Infrastructure.Auth;
using Wayd.Infrastructure.Messaging;
using Wolverine;

namespace Wayd.Infrastructure.Tests.Sut.Messaging;

public sealed class UserIdentityMiddlewareTests
{
    [Fact]
    public void Before_WhenUserIdHeaderPresent_SeedsAmbientUserId()
    {
        // Arrange
        var envelope = new Envelope();
        envelope.Headers[UserIdentityHeaders.UserId] = "user-123";
        var ambientUserId = new AmbientUserId();

        // Act
        UserIdentityMiddleware.Before(envelope, ambientUserId);

        // Assert
        ambientUserId.Value.Should().Be("user-123");
    }

    [Fact]
    public void Before_WhenHeaderMissing_LeavesAmbientUserIdUnset()
    {
        // Arrange
        var envelope = new Envelope();
        var ambientUserId = new AmbientUserId();

        // Act
        UserIdentityMiddleware.Before(envelope, ambientUserId);

        // Assert
        ambientUserId.Value.Should().BeNull();
    }

    [Fact]
    public void Before_WhenHeaderPresentButEmpty_LeavesAmbientUserIdUnset()
    {
        // Arrange
        var envelope = new Envelope();
        envelope.Headers[UserIdentityHeaders.UserId] = string.Empty;
        var ambientUserId = new AmbientUserId();

        // Act
        UserIdentityMiddleware.Before(envelope, ambientUserId);

        // Assert
        ambientUserId.Value.Should().BeNull();
    }
}
