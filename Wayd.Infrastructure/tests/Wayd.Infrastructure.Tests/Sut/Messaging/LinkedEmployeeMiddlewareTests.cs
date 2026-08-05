using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Messaging;
using Wolverine;

namespace Wayd.Infrastructure.Tests.Sut.Messaging;

public sealed class LinkedEmployeeMiddlewareTests
{
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    private sealed record GatedMessage : IRequireLinkedEmployee;

    private sealed record UngatedMessage;

    private static Envelope EnvelopeFor(object message) => new() { Message = message };

    [Fact]
    public async Task Before_ForLinkedUser_Proceeds()
    {
        // Arrange
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.User);
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = () => LinkedEmployeeMiddleware.Before(
            EnvelopeFor(new GatedMessage()), _currentUser.Object, _currentPrincipal.Object, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Before_ForUnlinkedUser_ThrowsForbiddenWithActionableMessage()
    {
        // Arrange
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.User);
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        // Act
        var act = () => LinkedEmployeeMiddleware.Before(
            EnvelopeFor(new GatedMessage()), _currentUser.Object, _currentPrincipal.Object, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ForbiddenException>())
            .WithMessage("*isn't linked to an employee record*")
            .WithMessage("*administrator*");
    }

    [Fact]
    public async Task Before_ForUnmarkedMessage_ProceedsWithoutResolvingTheLink()
    {
        // Arrange — the gate must cost nothing for the messages that do not opt in.
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.User);

        // Act
        var act = () => LinkedEmployeeMiddleware.Before(
            EnvelopeFor(new UngatedMessage()), _currentUser.Object, _currentPrincipal.Object, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _currentPrincipal.Verify(p => p.GetEmployeeId(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Before_ForSystemActor_ProceedsWithoutResolvingTheLink()
    {
        // Arrange — background work is not a person and holds no link, but must still be able to
        // dispatch these messages (a durable re-dispatch of a user-originated command runs here).
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.System);

        // Act
        var act = () => LinkedEmployeeMiddleware.Before(
            EnvelopeFor(new GatedMessage()), _currentUser.Object, _currentPrincipal.Object, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _currentPrincipal.Verify(p => p.GetEmployeeId(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Before_ForAnonymousActor_ProceedsWithoutResolvingTheLink()
    {
        // Arrange — an unauthenticated caller is rejected by the permission gate, not by this one;
        // reporting "link your account" to someone who is not signed in would be misleading.
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.Anonymous);

        // Act
        var act = () => LinkedEmployeeMiddleware.Before(
            EnvelopeFor(new GatedMessage()), _currentUser.Object, _currentPrincipal.Object, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _currentPrincipal.Verify(p => p.GetEmployeeId(It.IsAny<CancellationToken>()), Times.Never);
    }
}
