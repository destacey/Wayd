using Wayd.Common.Domain.Tagging;
using Wayd.Common.Domain.Tests.Data.Models;

namespace Wayd.Common.Domain.Tests.Sut.Tagging;

public sealed class TagSetTests
{
    private static TestTagAssignment Assign(Guid tagId, Guid? categoryId) => new(tagId, categoryId);

    #region Apply

    [Fact]
    public void Apply_InACategoryTakingOne_ReplacesTheTagHeldThere()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: false, "ios", "android");
        List<TestTagAssignment> sut = [Assign(category["ios"].Id, category.Id)];

        // Act
        var change = TagSet.Apply(sut, category["android"], category, Assign);

        // Assert
        change.Should().NotBeNull();
        change!.Added.Should().Equal(category["android"].Id);
        change.Removed.Should().Equal(category["ios"].Id);
        change.TagIds.Should().Equal(category["android"].Id);
    }

    [Fact]
    public void Apply_WhenAlreadyCarried_ChangesNothing()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: true, "ios");
        List<TestTagAssignment> sut = [Assign(category["ios"].Id, category.Id)];

        // Act
        var change = TagSet.Apply(sut, category["ios"], category, Assign);

        // Assert
        change.Should().BeNull();
        sut.Should().ContainSingle();
    }

    [Fact]
    public void Apply_AFreeTag_JoinsTheOthersWithNoCategory()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: false, "ios");
        var free = TestTag.CreateFree("migration wave").Value;
        List<TestTagAssignment> sut = [Assign(category["ios"].Id, category.Id)];

        // Act
        var change = TagSet.Apply(sut, free, null, Assign);

        // Assert
        // A free tag is in no category, so it never displaces a tag in one, even in a category that takes one.
        change!.Added.Should().Equal(free.Id);
        change.Removed.Should().BeEmpty();
        sut.Should().HaveCount(2);
        sut.Single(a => a.TagId == free.Id).CategoryId.Should().BeNull();
    }

    [Fact]
    public void Apply_AFreeTagWithACategory_Throws()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: true);
        var free = TestTag.CreateFree("migration wave").Value;

        // Act
        var act = () => TagSet.Apply([], free, category, Assign);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apply_ATagInACategoryWithoutIt_Throws()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: true, "ios");

        // Act
        var act = () => TagSet.Apply([], category["ios"], null, Assign);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion Apply

    #region Remove

    [Fact]
    public void Remove_WhenCarried_ReportsTheRemaining()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: true, "ios", "android");
        List<TestTagAssignment> sut = [Assign(category["ios"].Id, category.Id), Assign(category["android"].Id, category.Id)];

        // Act
        var change = TagSet.Remove(sut, category["ios"].Id);

        // Assert
        change!.Removed.Should().Equal(category["ios"].Id);
        change.TagIds.Should().Equal(category["android"].Id);
    }

    [Fact]
    public void Remove_WhenNotCarried_ChangesNothing()
    {
        // Act
        var change = TagSet.Remove(new List<TestTagAssignment>(), Guid.NewGuid());

        // Assert
        change.Should().BeNull();
    }

    #endregion Remove

    #region Replace

    [Fact]
    public void Replace_SwapsTheTagInOneChange()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: true, "payments");
        var free = TestTag.CreateFree("payment").Value;
        List<TestTagAssignment> sut = [Assign(free.Id, null)];

        // Act
        var change = TagSet.Replace(sut, free.Id, category["payments"], category, Assign);

        // Assert
        change!.Added.Should().Equal(category["payments"].Id);
        change.Removed.Should().Equal(free.Id);
        sut.Should().ContainSingle().Which.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public void Replace_WhenTheNewTagIsAlreadyCarried_OnlyRemovesTheOld()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: true, "payments");
        var free = TestTag.CreateFree("payment").Value;
        List<TestTagAssignment> sut = [Assign(free.Id, null), Assign(category["payments"].Id, category.Id)];

        // Act
        var change = TagSet.Replace(sut, free.Id, category["payments"], category, Assign);

        // Assert
        change!.Added.Should().BeEmpty();
        change.Removed.Should().Equal(free.Id);
        change.TagIds.Should().Equal(category["payments"].Id);
    }

    [Fact]
    public void Replace_InACategoryTakingOne_AlsoRemovesTheTagHeldThere()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: false, "ios", "android");
        var free = TestTag.CreateFree("droid").Value;
        List<TestTagAssignment> sut = [Assign(free.Id, null), Assign(category["ios"].Id, category.Id)];

        // Act
        var change = TagSet.Replace(sut, free.Id, category["android"], category, Assign);

        // Assert
        change!.Removed.Should().BeEquivalentTo([free.Id, category["ios"].Id]);
        change.TagIds.Should().Equal(category["android"].Id);
    }

    [Fact]
    public void Replace_WhenTheOldTagIsNotCarried_ChangesNothing()
    {
        // Arrange
        var category = TestTagCategory.Create(allowsMany: true, "payments");
        List<TestTagAssignment> sut = [];

        // Act
        var change = TagSet.Replace(sut, Guid.NewGuid(), category["payments"], category, Assign);

        // Assert
        change.Should().BeNull();
        sut.Should().BeEmpty();
    }

    #endregion Replace
}
