using Wayd.Common.Domain.Tagging;
using Wayd.Common.Domain.Tests.Data.Models;

namespace Wayd.Common.Domain.Tests.Sut.Tagging;

public sealed class TagSetTests
{
    private static TestTagAssignment Assign(Guid tagId, Guid? categoryId) => new(tagId, categoryId);

    #region Apply

    [Fact]
    public void Apply_OnAnAxisTakingOne_ReplacesTheTagHeldThere()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: false, "ios", "android");
        List<TestTagAssignment> sut = [Assign(axis["ios"].Id, axis.Id)];

        // Act
        var change = TagSet.Apply(sut, axis["android"], axis, Assign);

        // Assert
        change.Should().NotBeNull();
        change!.Added.Should().Equal(axis["android"].Id);
        change.Removed.Should().Equal(axis["ios"].Id);
        change.TagIds.Should().Equal(axis["android"].Id);
    }

    [Fact]
    public void Apply_WhenAlreadyCarried_ChangesNothing()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: true, "ios");
        List<TestTagAssignment> sut = [Assign(axis["ios"].Id, axis.Id)];

        // Act
        var change = TagSet.Apply(sut, axis["ios"], axis, Assign);

        // Assert
        change.Should().BeNull();
        sut.Should().ContainSingle();
    }

    [Fact]
    public void Apply_AFreeTag_JoinsTheOthersWithNoAxis()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: false, "ios");
        var free = TestTag.CreateFree("migration wave").Value;
        List<TestTagAssignment> sut = [Assign(axis["ios"].Id, axis.Id)];

        // Act
        var change = TagSet.Apply(sut, free, null, Assign);

        // Assert
        // A free tag is on no axis, so it never displaces a tag on one, even on an axis that takes one.
        change!.Added.Should().Equal(free.Id);
        change.Removed.Should().BeEmpty();
        sut.Should().HaveCount(2);
        sut.Single(a => a.TagId == free.Id).CategoryId.Should().BeNull();
    }

    [Fact]
    public void Apply_AFreeTagWithAnAxis_Throws()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: true);
        var free = TestTag.CreateFree("migration wave").Value;

        // Act
        var act = () => TagSet.Apply([], free, axis, Assign);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apply_ATagOnAnAxisWithoutIt_Throws()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: true, "ios");

        // Act
        var act = () => TagSet.Apply([], axis["ios"], null, Assign);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion Apply

    #region Remove

    [Fact]
    public void Remove_WhenCarried_ReportsTheRemaining()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: true, "ios", "android");
        List<TestTagAssignment> sut = [Assign(axis["ios"].Id, axis.Id), Assign(axis["android"].Id, axis.Id)];

        // Act
        var change = TagSet.Remove(sut, axis["ios"].Id);

        // Assert
        change!.Removed.Should().Equal(axis["ios"].Id);
        change.TagIds.Should().Equal(axis["android"].Id);
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
        var axis = TestTagCategory.Create(allowsMany: true, "payments");
        var free = TestTag.CreateFree("payment").Value;
        List<TestTagAssignment> sut = [Assign(free.Id, null)];

        // Act
        var change = TagSet.Replace(sut, free.Id, axis["payments"], axis, Assign);

        // Assert
        change!.Added.Should().Equal(axis["payments"].Id);
        change.Removed.Should().Equal(free.Id);
        sut.Should().ContainSingle().Which.CategoryId.Should().Be(axis.Id);
    }

    [Fact]
    public void Replace_WhenTheNewTagIsAlreadyCarried_OnlyRemovesTheOld()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: true, "payments");
        var free = TestTag.CreateFree("payment").Value;
        List<TestTagAssignment> sut = [Assign(free.Id, null), Assign(axis["payments"].Id, axis.Id)];

        // Act
        var change = TagSet.Replace(sut, free.Id, axis["payments"], axis, Assign);

        // Assert
        change!.Added.Should().BeEmpty();
        change.Removed.Should().Equal(free.Id);
        change.TagIds.Should().Equal(axis["payments"].Id);
    }

    [Fact]
    public void Replace_OnAnAxisTakingOne_AlsoRemovesTheTagHeldThere()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: false, "ios", "android");
        var free = TestTag.CreateFree("droid").Value;
        List<TestTagAssignment> sut = [Assign(free.Id, null), Assign(axis["ios"].Id, axis.Id)];

        // Act
        var change = TagSet.Replace(sut, free.Id, axis["android"], axis, Assign);

        // Assert
        change!.Removed.Should().BeEquivalentTo([free.Id, axis["ios"].Id]);
        change.TagIds.Should().Equal(axis["android"].Id);
    }

    [Fact]
    public void Replace_WhenTheOldTagIsNotCarried_ChangesNothing()
    {
        // Arrange
        var axis = TestTagCategory.Create(allowsMany: true, "payments");
        List<TestTagAssignment> sut = [];

        // Act
        var change = TagSet.Replace(sut, Guid.NewGuid(), axis["payments"], axis, Assign);

        // Assert
        change.Should().BeNull();
        sut.Should().BeEmpty();
    }

    #endregion Replace
}
