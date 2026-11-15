using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Tests.Sut.Models;

/// <remarks>
/// These run the map over in-memory rows, where a text operator other than equality throws on a null value;
/// SQL Server has no such limit, and the integration tests prove what the map translates to there.
/// </remarks>
public sealed class ListFieldMapTests
{
    public enum Stage { Draft, Active, Closed }

    public sealed record Item(int Id, string Name, string? Note, Stage Stage, int Score, Instant? At, LocalDate? Due);

    private static readonly ListFieldMap<Item> Map =
        ListFieldMap<Item>.Create("name", i => i.Id)
            .Map("name", i => i.Name)
            .Map("note", i => i.Note)
            .Map("stage", i => i.Stage)
            .Map("score", i => i.Score)
            .Map("at", i => i.At)
            .Map("due", i => i.Due);

    private static readonly List<Item> Items =
    [
        new(1, "vendor review", "late", Stage.Active, 10, Instant.FromUtc(2026, 3, 1, 9, 0), new LocalDate(2026, 3, 1)),
        new(2, "budget", null, Stage.Draft, 20, Instant.FromUtc(2026, 3, 2, 9, 0), new LocalDate(2026, 3, 2)),
        new(3, "a,b|c(d)\\e", "", Stage.Closed, 30, null, null),
        new(4, "null", null, Stage.Active, 20, Instant.FromUtc(2026, 3, 1, 23, 59), new LocalDate(2026, 3, 1)),
    ];

    private static List<int> Filter(string filter) =>
        [.. Map.ApplyFilter(Items.AsQueryable(), filter).Select(i => i.Id)];

    private static List<int> Sort(string? sort) =>
        [.. Map.ApplySort(Items.AsQueryable(), sort).Select(i => i.Id)];

    [Theory]
    [InlineData("name=*vend", new[] { 1 })]
    [InlineData("name!*vend", new[] { 2, 3, 4 })]
    [InlineData("name^bud", new[] { 2 })]
    [InlineData("name$view", new[] { 1 })]
    [InlineData("name=budget", new[] { 2 })]
    [InlineData("name!=budget", new[] { 1, 3, 4 })]
    [InlineData("(score>=15,score<=25)", new[] { 2, 4 })]
    [InlineData("(stage=Active|stage=Closed)", new[] { 1, 3, 4 })]
    [InlineData("stage=active", new[] { 1, 4 })]
    [InlineData("(stage=Active,score=20)|stage=Draft", new[] { 2, 4 })]
    public void ApplyFilter_MatchesTheGridsOperators(string filter, int[] expected)
    {
        // Act
        var ids = Filter(filter);

        // Assert
        ids.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ApplyFilter_TreatsNullAndEmptyTextAsBlank()
    {
        // Act
        var blank = Filter("note=");
        var notBlank = Filter("note!=");

        // Assert
        blank.Should().BeEquivalentTo([2, 3, 4]);
        notBlank.Should().BeEquivalentTo([1]);
    }

    [Fact]
    public void ApplyFilter_ReadsTheWordNullAsText()
    {
        // Act — a blank is `name=`, so the word itself must stay searchable
        var ids = Filter("name=null");

        // Assert
        ids.Should().BeEquivalentTo([4]);
    }

    [Fact]
    public void ApplyFilter_MatchesEscapedSpecialCharactersLiterally()
    {
        // Act
        var ids = Filter(@"name=a\,b\|c\(d\)\\e");

        // Assert
        ids.Should().BeEquivalentTo([3]);
    }

    [Fact]
    public void ApplyFilter_ComparesInstantsAsIsoInstants()
    {
        // Act — one UTC day, as the grid sends a date-time column's "equals"
        var ids = Filter("(at>=2026-03-01T00:00:00Z,at<2026-03-02T00:00:00.000Z)");

        // Assert
        ids.Should().BeEquivalentTo([1, 4]);
    }

    [Fact]
    public void ApplyFilter_ComparesLocalDatesAsIsoDates()
    {
        // Act
        var range = Filter("(due>=2026-03-02,due<2026-03-03)");
        var blank = Filter("due=");

        // Assert
        range.Should().BeEquivalentTo([2]);
        blank.Should().BeEquivalentTo([3]);
    }

    [Fact]
    public void ApplyFilter_ReturnsTheSourceWhenThereIsNoFilter()
    {
        // Act
        var ids = Map.ApplyFilter(Items.AsQueryable(), "  ").Select(i => i.Id).ToList();

        // Assert
        ids.Should().BeEquivalentTo([1, 2, 3, 4]);
    }

    [Fact]
    public void ApplySort_UsesTheDefaultSortWhenThereIsNone()
    {
        // Act
        var ids = Sort(null);

        // Assert — by name: "a,b|c(d)\e", "budget", "null", "vendor review"
        ids.Should().Equal(3, 2, 4, 1);
    }

    [Fact]
    public void ApplySort_SortsByEveryNamedFieldThenTheUniqueKey()
    {
        // Act — ids 2 and 4 tie on score, and the unique key keeps them in one order
        var ids = Sort("score desc");
        var multi = Sort("stage asc, score desc");

        // Assert
        ids.Should().Equal(3, 2, 4, 1);
        multi.Should().Equal(2, 4, 1, 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("name=*x")]
    [InlineData(@"name=\(")]
    [InlineData("at=")]
    public void FilterError_AcceptsAFilterTheMapCanApply(string? filter)
    {
        // Act
        var error = Map.FilterError(filter);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void FilterError_RefusesAFieldTheMapDoesNotName_AndListsTheFieldsItDoes()
    {
        // Act
        var error = Map.FilterError("id=1");

        // Assert
        error.Should().Contain("'filter' names a field this list cannot use").And.Contain("name, note, stage, score, at, due");
    }

    [Theory]
    [InlineData("name=(")]
    [InlineData("name=a,b")]
    [InlineData("score=many")]
    [InlineData("at=yesterday")]
    [InlineData("due=2026-13-01")]
    [InlineData("stage=Exploded")]
    [InlineData("stage=1")]
    [InlineData("stage=+1")]
    [InlineData(@"stage=Draft\,Active")]
    public void FilterError_RefusesAStringItCannotRead(string filter)
    {
        // Act
        var error = Map.FilterError(filter);

        // Assert
        error.Should().StartWith("'filter' could not be read");
    }

    [Theory]
    [InlineData("score=many")]
    [InlineData("score=99999999999")]
    [InlineData("score=1.5")]
    [InlineData("stage>Draft")]
    [InlineData("stage=*Dra")]
    [InlineData("at>yesterday")]
    [InlineData("name=(")]
    [InlineData("(name=a")]
    [InlineData("name=a,")]
    public void FilterError_DescribesTheInputWithoutNamingServerTypes(string filter)
    {
        // Act
        var error = Map.FilterError(filter);

        // Assert
        error.Should().NotBeNull().And.NotContain("System.").And.NotContain("Wayd.");
    }

    [Fact]
    public void FilterError_ListsAnEnumFieldsCodes()
    {
        // Act
        var error = Map.FilterError("stage=Exploded");

        // Assert
        error.Should().Contain("'Exploded' is not one of Draft, Active, Closed");
    }

    [Theory]
    [InlineData("name=*vendor/i")]
    [InlineData("name=a/i,score=1")]
    [InlineData("(name=a/i)")]
    [InlineData("name=a/i ,score=1")]
    [InlineData("name=a/i ")]
    public void FilterError_RefusesTheCaseInsensitiveFlag(string filter)
    {
        // Act
        var error = Map.FilterError(filter);

        // Assert
        error.Should().Contain("/i");
    }

    [Fact]
    public void FilterError_AcceptsAnEscapedSlashBeforeAnI()
    {
        // Act
        var error = Map.FilterError(@"name=a\/i");

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void FilterError_RefusesAFilterLongerThanTheMaximum()
    {
        // Arrange
        var filter = "name=" + new string('x', ListFieldMap<Item>.MaxFilterLength);

        // Act
        var error = Map.FilterError(filter);

        // Assert
        error.Should().Contain("longer than");
    }

    [Fact]
    public void FilterError_RefusesMoreConditionsThanTheMaximum()
    {
        // Arrange — escaped separators are text, not conditions
        var atLimit = string.Join("|", Enumerable.Repeat(@"name=a\,b", ListFieldMap<Item>.MaxFilterConditions));
        var overLimit = atLimit + "|name=c";

        // Act
        var accepted = Map.FilterError(atLimit);
        var refused = Map.FilterError(overLimit);

        // Assert
        accepted.Should().BeNull();
        refused.Should().Contain("more than");
    }

    [Fact]
    public void FilterError_RefusesNestingDeeperThanTheMaximum()
    {
        // Arrange
        var depth = ListFieldMap<Item>.MaxFilterDepth + 1;
        var filter = new string('(', depth) + "name=a" + new string(')', depth);

        // Act
        var error = Map.FilterError(filter);

        // Assert
        error.Should().Contain("nests parentheses");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("name")]
    [InlineData("stage asc, score desc")]
    [InlineData("stage ASC, score Desc")]
    public void SortError_AcceptsASortTheMapCanApply(string? sort)
    {
        // Act
        var error = Map.SortError(sort);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ApplySort_ReadsADirectionInAnyCase()
    {
        // Act
        var ids = Sort("score DESC, name Asc");

        // Assert
        ids.Should().Equal(3, 2, 4, 1);
    }

    [Fact]
    public void SortError_RefusesAFieldTheMapDoesNotName()
    {
        // Act
        var error = Map.SortError("id desc");

        // Assert
        error.Should().Contain("'sort' names a field this list cannot use");
    }

    [Fact]
    public void SortError_RefusesMoreFieldsThanTheMaximum()
    {
        // Arrange
        var sort = string.Join(", ", Enumerable.Repeat("name", ListFieldMap<Item>.MaxSortFields + 1));

        // Act
        var error = Map.SortError(sort);

        // Assert
        error.Should().Contain("more than");
    }

    [Fact]
    public void SortError_RefusesASortLongerThanTheMaximum()
    {
        // Arrange
        var sort = "name" + new string(' ', ListFieldMap<Item>.MaxSortLength) + "asc";

        // Act
        var error = Map.SortError(sort);

        // Assert
        error.Should().Contain("longer than");
    }

    [Theory]
    [InlineData("name sideways")]
    [InlineData("name asc,")]
    public void SortError_DescribesTheInputWithoutNamingServerTypes(string sort)
    {
        // Act
        var error = Map.SortError(sort);

        // Assert
        error.Should().NotBeNull().And.NotContain("System.").And.NotContain("Wayd.");
    }
}
