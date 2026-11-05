using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Commands;

public sealed class PostDiscussionMessageCommandHandlerTests
{
    private const string GadgetType = "test.discussion-post.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 29, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _gadgets = new(GadgetType);

    public PostDiscussionMessageCommandHandlerTests()
    {
        DiscussionChannelTypes.Register(Gadget);
    }

    private PostDiscussionMessageCommandHandler CreateHandler()
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var writer = new DiscussionWriter(_dbContext, [_gadgets], principal.Object, Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"), new TestingDateTimeProvider(new FakeClock(Now)));
        return new PostDiscussionMessageCommandHandler(writer, _dbContext);
    }

    [Fact]
    public async Task Handle_ShouldSaveThePost()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();

        // Act
        var result = await CreateHandler().Handle(new PostDiscussionMessageCommand(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Hello"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.DiscussionMessages.Single().Id.Should().Be(result.Value);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSaveNothing_WhenThePostIsRefused()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await CreateHandler().Handle(new PostDiscussionMessageCommand(GadgetType, ownerId, DiscussionMessageKind.InternalNote, null, "Between us"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.CannotWrite);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReportAConflict_WhenAnotherFirstPostOpenedTheConversationAtOnce()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        _dbContext.FailNextSaveWith = new DbUpdateException("Cannot insert duplicate key row in object 'Discussions.Channels'.");

        // Act
        var result = await CreateHandler().Handle(new PostDiscussionMessageCommand(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Hello"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.Conflict);
    }
}
