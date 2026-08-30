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
        var category = ProductTagCategory.Create("Platform", null, allowsMany, 1);

        return (category, category.AddTag("ios").Value, category.AddTag("android").Value);
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
    public void Tag_ShouldFail_WhenTheTagIsNotOnTheSuppliedAxis()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, _) = Axis(allowsMany: true);
        var other = ProductTagCategory.Create("Tech Stack", null, true, 2);

        // Act
        var result = sut.Tag(ios, other, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("That tag does not belong to the supplied axis.");
    }

    [Fact]
    public void Tag_ShouldFail_WhenTheTagIsInactive()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, _) = Axis(allowsMany: true);
        ios.Deactivate();

        // Act
        var result = sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Deactivating retires a tag from new use without stripping it from what already carries it.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An inactive tag cannot be applied.");
    }

    [Fact]
    public void AddTag_ShouldFail_OnASystemAxis()
    {
        // Arrange
        var seeded = ProductTagCategory.CreateSystem("Platform", null, true, 1);

        // Act
        var result = seeded.AddTag("ios");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System tag categories cannot be modified.");
    }

    [Fact]
    public void AddTag_ShouldFail_OnADuplicateName()
    {
        // Arrange
        var category = ProductTagCategory.Create("Platform", null, true, 1);
        category.AddTag("ios");

        // Act
        var result = category.AddTag("iOS");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A tag named 'iOS' already exists on this axis.");
    }

    [Fact]
    public void RenameTag_ShouldRenameIt()
    {
        // Arrange
        var category = ProductTagCategory.Create("Platform", null, true, 1);
        var tag = category.AddTag("ios").Value;

        // Act
        var result = category.RenameTag(tag.Id, "iOS");

        // Assert
        // Products reference the tag by id, so the new name shows everywhere at once — which is the
        // point of a curated list over free text.
        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("iOS");
    }

    [Fact]
    public void RenameTag_ShouldFail_OnADuplicateName()
    {
        // Arrange
        var category = ProductTagCategory.Create("Platform", null, true, 1);
        category.AddTag("ios");
        var android = category.AddTag("android").Value;

        // Act
        var result = category.RenameTag(android.Id, "IOS");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A tag named 'IOS' already exists on this axis.");
    }

    [Fact]
    public void RenameTag_ShouldAllowATagToKeepItsOwnName()
    {
        // Arrange
        var category = ProductTagCategory.Create("Platform", null, true, 1);
        var tag = category.AddTag("ios").Value;

        // Act
        var result = category.RenameTag(tag.Id, "ios", "Apple mobile.");

        // Assert
        // The uniqueness check must exclude the tag being renamed, or editing only the description
        // would be refused.
        result.IsSuccess.Should().BeTrue();
        tag.Description.Should().Be("Apple mobile.");
    }

    [Fact]
    public void RenameTag_ShouldFail_ForATagOnAnotherAxis()
    {
        // Arrange
        var category = ProductTagCategory.Create("Platform", null, true, 1);
        var other = ProductTagCategory.Create("Tech Stack", null, true, 2);
        var foreign = other.AddTag("dotnet").Value;

        // Act
        var result = category.RenameTag(foreign.Id, "net");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("That tag does not belong to this axis.");
    }

    [Fact]
    public void RenameTag_ShouldFail_OnASystemAxis()
    {
        // Arrange
        var seeded = ProductTagCategory.CreateSystem("Platform", null, true, 1);
        var tag = seeded.AddSystemTag("ios");

        // Act
        var result = seeded.RenameTag(tag.Id, "iOS");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System tag categories cannot be modified.");
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
    public void Tag_ShouldRaiseAnEventCarryingTheWholeResultingSet()
    {
        // Arrange
        var sut = _faker.Generate();
        var (platform, ios, android) = Axis(allowsMany: true);

        // Act
        sut.Tag(ios, platform, EventActor.System, _dateTimeProvider.Now);
        sut.Tag(android, platform, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The current state, not a diff — a projection should not have to work out what changed.
        var changed = sut.DomainEvents.OfType<ProductTagsChangedEvent>().Last();
        changed.TagIds.Should().BeEquivalentTo([ios.Id, android.Id]);
    }

    #endregion Events
}
