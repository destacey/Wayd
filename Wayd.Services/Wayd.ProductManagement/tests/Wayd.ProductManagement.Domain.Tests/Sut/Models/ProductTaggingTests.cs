using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

/// <summary>
/// Tagging a product along an axis. Tags describe; the type decides behaviour, so no tag can change
/// what a node is allowed to do.
/// </summary>
public sealed class ProductTaggingTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ProductFaker _faker;

    public ProductTaggingTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new ProductFaker();
    }

    private static (ProductTagCategory Category, ProductTag First, ProductTag Second) Axis(bool allowsMany)
    {
        var category = new ProductTagCategoryFaker().WithAllowsMany(allowsMany).WithTags("ios", "android").Generate();

        return (category, category.Tags.Single(t => t.Name == "ios"), category.Tags.Single(t => t.Name == "android"));
    }

    #region Applying tags

    [Fact]
    public void Tag_ShouldApplyTheTag()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, _) = Axis(allowsMany: true);

        // Act
        var result = sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Tags.Should().ContainSingle().Which.TagId.Should().Be(ios.Id);
    }

    [Fact]
    public void Tag_ShouldKeepBoth_WhenTheAxisAllowsMany()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, android) = Axis(allowsMany: true);

        // Act
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);
        sut.Tag(android, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A cross-platform app genuinely targets both; forcing a choice would record something false.
        sut.Tags.Should().HaveCount(2);
    }

    [Fact]
    public void Tag_ShouldReplace_WhenTheAxisAllowsOne()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, android) = Axis(allowsMany: false);

        // Act
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);
        sut.Tag(android, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // "This is Android, not iOS" is a correction, not an error.
        sut.Tags.Should().ContainSingle().Which.TagId.Should().Be(android.Id);
    }

    [Fact]
    public void Tag_ShouldDenormalizeTheAxis()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, _) = Axis(allowsMany: true);

        // Act
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // "Every product with a Platform tag" needs no join through the tag.
        sut.Tags.Single().CategoryId.Should().Be(platform.Id);
    }

    [Fact]
    public void Tag_ShouldBeIdempotent()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, _) = Axis(allowsMany: true);
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Tags.Should().ContainSingle();
    }

    #endregion Applying tags

    #region Refusals

    [Fact]
    public void Tag_ShouldThrow_WhenTheTagIsNotOnTheSuppliedAxis()
    {
        // Arrange
        var sut = _faker.Generate();
        var (_, ios, _) = Axis(allowsMany: true);
        var other = new ProductTagCategoryFaker().Generate();

        // Act
        var act = () => sut.Tag(ios, other, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The caller loads the axis from the tag, so a mismatch is a bug in the caller, not a request to refuse.
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tag_ShouldFail_WhenTheTagIsInactive()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, _) = Axis(allowsMany: true);
        platform.DeactivateTag(ios.Id, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Deactivating retires a tag from new use without stripping it from what already carries it.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Product.InactiveTagError);
    }

    #endregion Refusals

    #region Removing

    [Fact]
    public void Untag_ShouldRemoveIt()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, _) = Axis(allowsMany: true);
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.Untag(ios.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Untag_ShouldRaiseNothing_WhenTheTagWasNotCarried()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Untag(Guid.CreateVersion7(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        // An event asserts something happened; removing a tag that was not there did not.
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Removing

    #region Events

    [Fact]
    public void Tag_ShouldRaiseAnEventCarryingTheChangeAndTheWholeResultingSet()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, android) = Axis(allowsMany: true);

        // Act
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);
        sut.Tag(android, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var changed = sut.DomainEvents.OfType<ProductTagsChangedEventV2>().Last();
        changed.Added.Should().BeEquivalentTo([android.Id]);
        changed.Removed.Should().BeEmpty();
        changed.TagIds.Should().BeEquivalentTo([ios.Id, android.Id]);
    }

    [Fact]
    public void Tag_WhenTheAxisAllowsOne_ShouldReportTheReplacedTagAsRemoved()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, android) = Axis(allowsMany: false);
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);

        // Act
        sut.Tag(android, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var changed = sut.DomainEvents.OfType<ProductTagsChangedEventV2>().Last();
        changed.Added.Should().BeEquivalentTo([android.Id]);
        changed.Removed.Should().BeEquivalentTo([ios.Id]);
        changed.TagIds.Should().BeEquivalentTo([android.Id]);
    }

    [Fact]
    public void Untag_ShouldRaiseAnEventCarryingTheRemovedTag()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, android) = Axis(allowsMany: true);
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);
        sut.Tag(android, platform, EventActor.System, _dateTimeProvider.Now);

        // Act
        sut.Untag(ios.Id, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var changed = sut.DomainEvents.OfType<ProductTagsChangedEventV2>().Last();
        changed.Added.Should().BeEmpty();
        changed.Removed.Should().BeEquivalentTo([ios.Id]);
        changed.TagIds.Should().BeEquivalentTo([android.Id]);
    }

    #endregion Events
}
