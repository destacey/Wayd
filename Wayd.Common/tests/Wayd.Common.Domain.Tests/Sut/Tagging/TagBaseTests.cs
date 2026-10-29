using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tagging;
using Wayd.Common.Domain.Tests.Data.Models;

namespace Wayd.Common.Domain.Tests.Sut.Tagging;

public sealed class TagBaseTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 28, 9, 0, 0);

    #region ValidateFreeName

    [Theory]
    [InlineData("  migration   wave 2 ", "migration wave 2")]
    [InlineData("follow\tup", "follow up")]
    public void ValidateFreeName_TrimsAndCollapsesWhitespace(string typed, string expected)
    {
        // Act
        var result = TagBase.ValidateFreeName(typed);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void ValidateFreeName_WhenBlank_Fails(string? typed)
    {
        // Act
        var result = TagBase.ValidateFreeName(typed);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.TagNameRequired);
    }

    [Fact]
    public void ValidateFreeName_WhenTooLong_Fails()
    {
        // Act
        var result = TagBase.ValidateFreeName(new string('a', TagBase.NameMaxLength + 1));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.TagNameTooLong);
    }

    #endregion ValidateFreeName

    #region Free tags

    [Fact]
    public void CreateFree_IsAnActiveTagOnNoAxis()
    {
        // Act
        var sut = TestTag.CreateFree(" migration  wave ").Value;

        // Assert
        sut.IsFree.Should().BeTrue();
        sut.CategoryId.Should().BeNull();
        sut.IsActive.Should().BeTrue();
        sut.Name.Should().Be("migration wave");
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TestTagEvent>().Which.Kind.Should().Be("FreeTagCreated");
    }

    [Fact]
    public void UpdateFreeTagDetails_WhenChanged_RenamesAndRaises()
    {
        // Arrange
        var sut = TestTag.CreateFree("migration wave").Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.UpdateFreeTagDetails("Migration  wave 2", "Second cut-over", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Migration wave 2");
        sut.Description.Should().Be("Second cut-over");
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TestTagEvent>().Which.Kind.Should().Be("FreeTagDetailsUpdated");
    }

    [Fact]
    public void UpdateFreeTagDetails_WhenUnchanged_RaisesNothing()
    {
        // Arrange
        var sut = TestTag.CreateFree("migration wave").Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.UpdateFreeTagDetails(" migration wave ", null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateFreeTagDetails_OnATagOnAnAxis_Fails()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: true, "ios");

        // Act
        var result = axis["ios"].UpdateFreeTagDetails("iOS", null, EventActor.System, Now);

        // Assert
        // A tag on an axis is renamed through the axis, which keeps names unique on it.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.NotAFreeTag);
        axis["ios"].Name.Should().Be("ios");
    }

    [Fact]
    public void DeleteFreeTag_RaisesWithTheTagItWasMergedInto()
    {
        // Arrange
        var sut = TestTag.CreateFree("payment").Value;
        sut.ClearDomainEvents();
        var target = Guid.NewGuid();

        // Act
        var result = sut.DeleteFreeTag(target, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TestTagEvent>().Subject;
        deleted.Kind.Should().Be("FreeTagDeleted");
        deleted.MergedIntoTagId.Should().Be(target);
    }

    [Fact]
    public void DeleteFreeTag_MergedIntoItself_Fails()
    {
        // Arrange
        var sut = TestTag.CreateFree("payment").Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.DeleteFreeTag(sut.Id, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.MergeIntoItself);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DeleteFreeTag_OnATagOnAnAxis_Fails()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: true, "ios");

        // Act
        var result = axis["ios"].DeleteFreeTag(null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.NotAFreeTag);
    }

    #endregion Free tags
}
