using FluentAssertions;
using Wayd.Common.Domain.Tagging;
using Wayd.ProductManagement.Application.ProductTags.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTags.Commands;

/// <summary>
/// Renaming a free tag, which keeps free tag names unique among themselves.
/// </summary>
public sealed class UpdateFreeProductTagCommandHandlerTests : ProductCommandTestBase
{
    private UpdateFreeProductTagCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<UpdateFreeProductTagCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRenameTheFreeTag()
    {
        // Arrange
        var free = SeedFreeTag("wave 2");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new UpdateFreeProductTagCommand(free.Id, "Wave 2", "Second migration wave"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        free.Name.Should().Be("Wave 2");
        free.Description.Should().Be("Second migration wave");
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnotherFreeTagsName()
    {
        // Arrange
        var free = SeedFreeTag("wave 2");
        SeedFreeTag("Wave 3");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new UpdateFreeProductTagCommand(free.Id, "wave 3", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.DuplicateFreeTagName);
        free.Name.Should().Be("wave 2");
    }

    [Fact]
    public async Task Handle_ShouldRefuseATagOnAnAxis()
    {
        // Arrange
        var (_, ios) = SeedTag();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new UpdateFreeProductTagCommand(ios.Id, "iOS", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.NotAFreeTag);
    }
}
