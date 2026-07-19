using CSharpFunctionalExtensions;
using FluentAssertions;
using Wayd.Common.Application.Dispatching;
using Wayd.Common.Application.Interfaces;
using Wolverine;

namespace Wayd.Common.Application.Tests.Sut.Dispatching;

public sealed class WolverineDispatcherTests
{
    private sealed record TestCommand : ICommand;
    private sealed record TestCommand<T>(T Value) : ICommand<T>;
    private sealed record TestQuery(int Value) : IQuery<int>;

    private readonly Mock<IMessageBus> _bus = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private WolverineDispatcher CreateDispatcher() => new(_bus.Object, _currentUser.Object);

    [Fact]
    public async Task Send_Command_WhenUserPresent_StampsUserIdHeader()
    {
        // Arrange
        _currentUser.Setup(u => u.GetUserId()).Returns("user-9");
        DeliveryOptions? captured = null;
        _bus.Setup(b => b.InvokeAsync<Result>(It.IsAny<object>(), It.IsAny<DeliveryOptions>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .Callback((object _, DeliveryOptions opts, CancellationToken _, TimeSpan? _) => captured = opts)
            .ReturnsAsync(Result.Success());

        // Act
        await CreateDispatcher().Send(new TestCommand(), TestContext.Current.CancellationToken);

        // Assert
        captured.Should().NotBeNull();
        captured!.Headers.Should().ContainKey(UserIdentityHeaders.UserId)
            .WhoseValue.Should().Be("user-9");
    }

    [Fact]
    public async Task Send_Command_WhenNoUser_UsesPlainOverloadWithoutDeliveryOptions()
    {
        // Arrange
        _currentUser.Setup(u => u.GetUserId()).Returns(string.Empty);
        _bus.Setup(b => b.InvokeAsync<Result>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(Result.Success());

        // Act
        await CreateDispatcher().Send(new TestCommand(), TestContext.Current.CancellationToken);

        // Assert — the DeliveryOptions overload must NOT be used when there is no user
        _bus.Verify(b => b.InvokeAsync<Result>(It.IsAny<object>(), It.IsAny<DeliveryOptions>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()), Times.Never);
        _bus.Verify(b => b.InvokeAsync<Result>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task Send_Command_InSystemScope_UsesPlainOverloadWithoutDeliveryOptions()
    {
        // Arrange — a system scope is self-identifying on the handling side, so no header is stamped
        // even though GetUserId() reports the well-known system id.
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.System);
        _currentUser.Setup(u => u.GetUserId()).Returns("11111111-1111-1111-1111-111111111111");
        _bus.Setup(b => b.InvokeAsync<Result>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(Result.Success());

        // Act
        await CreateDispatcher().Send(new TestCommand(), TestContext.Current.CancellationToken);

        // Assert
        _bus.Verify(b => b.InvokeAsync<Result>(It.IsAny<object>(), It.IsAny<DeliveryOptions>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()), Times.Never);
        _bus.Verify(b => b.InvokeAsync<Result>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task Send_Query_ReturnsBusResult_AndStampsHeaderWhenUserPresent()
    {
        // Arrange
        _currentUser.Setup(u => u.GetUserId()).Returns("user-42");
        DeliveryOptions? captured = null;
        _bus.Setup(b => b.InvokeAsync<int>(It.IsAny<object>(), It.IsAny<DeliveryOptions>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .Callback((object _, DeliveryOptions opts, CancellationToken _, TimeSpan? _) => captured = opts)
            .ReturnsAsync(7);

        // Act
        var result = await CreateDispatcher().Send(new TestQuery(1), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(7);
        captured!.Headers[UserIdentityHeaders.UserId].Should().Be("user-42");
    }

    [Fact]
    public async Task Send_CommandWithResponse_PassesMessageThroughAndReturnsResult()
    {
        // Arrange
        _currentUser.Setup(u => u.GetUserId()).Returns(string.Empty);
        _bus.Setup(b => b.InvokeAsync<Result<string>>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(Result.Success("ok"));

        // Act
        var result = await CreateDispatcher().Send(new TestCommand<string>("in"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
    }
}
