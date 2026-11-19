using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Models;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Common.Application.Tests.Sut.Models;

/// <remarks>
/// These run the map over in-memory rows, where a text operator other than equality throws on a null value;
/// SQL Server has no such limit, and the integration tests prove what the map translates to there.
/// </remarks>
public sealed class ListFieldMapTests
{
    public enum Stage { Draft, Active, Closed }

    public sealed record Item(int Id, string Name, string? Note, Stage Stage, int Score, Instant? At, LocalDate? Due);

    public sealed record Person(Guid Id, string Name);

    /// <summary>A row with people and tags, for the fields that filter by one member and sort or list by another.</summary>
    public sealed class Case
    {
        public required int Id { get; init; }
        public required Person Owner { get; init; }
        public Person? Lead { get; init; }
        public required Stage Stage { get; init; }
        public List<Person> Watchers { get; init; } = [];
    }

    private static readonly Person Ada = new(Guid.Parse("0196a8d5-0000-7000-8000-0000000000aa"), "Ada Lovelace");
    private static readonly Person Grace = new(Guid.Parse("0196a8d5-0000-7000-8000-000000000011"), "Grace Hopper");
    private static readonly Person Alan = new(Guid.Parse("0196a8d5-0000-7000-8000-0000000000cc"), "Alan Turing");

    private static readonly ListFieldMap<Case> CaseMap =
        ListFieldMap<Case>.Create("id", c => c.Id)
            .Map("id", c => c.Id)
            .Map("owner", c => c.Owner.Id, c => c.Owner.Name)
            .Map("lead", c => c.Lead == null ? (Guid?)null : c.Lead.Id)
            .Map("stage", c => c.Stage)
            .MapMany("watchers", c => c.Watchers.Select(w => w.Id))
            .Options("owner", cases => cases.Select(c => new ListFilterOptionRow<Guid> { Value = c.Owner.Id, Label = c.Owner.Name, Rank = 0 }))
            .Options("stage", cases => cases.Select(c => new ListFilterOptionRow<Stage>
            {
                Value = c.Stage,
                Label = c.Stage.ToString(),
                Rank = c.Stage == Stage.Closed ? 0 : 1,
            }))
            .Options("watchers", cases => cases
                .SelectMany(c => c.Watchers)
                .Select(w => new ListFilterOptionRow<Guid> { Value = w.Id, Label = w.Name, Rank = 0 }));

    private static readonly List<Case> Cases =
    [
        new() { Id = 1, Owner = Grace, Lead = null, Stage = Stage.Active, Watchers = [Ada] },
        new() { Id = 2, Owner = Ada, Lead = Grace, Stage = Stage.Draft, Watchers = [Ada, Alan] },
        new() { Id = 3, Owner = Grace, Lead = Ada, Stage = Stage.Closed, Watchers = [] },
    ];

    private static List<int> FilterCases(string filter) =>
        [.. CaseMap.ApplyFilter(Cases.AsQueryable(), filter).Select(c => c.Id)];

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

    [Fact]
    public void ApplyFilter_MatchesAFieldByTheMemberItFiltersBy()
    {
        // Act
        var ids = FilterCases($"owner={Grace.Id}");

        // Assert
        ids.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void ApplySort_OrdersAFieldByTheMemberItSortsBy()
    {
        // Act
        var ids = CaseMap.ApplySort(Cases.AsQueryable(), "owner asc").Select(c => c.Id).ToList();

        // Assert — Ada (2) before Grace (1, 3), though Grace's id is the smaller
        ids.Should().Equal(2, 1, 3);
    }

    [Fact]
    public void ApplyFilter_MatchesAnIdIgnoringCase()
    {
        // Act
        var ids = FilterCases($"owner={Ada.Id.ToString().ToUpperInvariant()}");

        // Assert
        ids.Should().Equal(2);
    }

    [Fact]
    public void ApplyFilter_MatchesABlankId()
    {
        // Act
        var ids = FilterCases($"lead=|lead={Ada.Id}");

        // Assert
        ids.Should().BeEquivalentTo([1, 3]);
    }

    [Theory]
    [InlineData("owner=nope")]
    [InlineData("owner=0196a8d50000700080000000000000aa")]
    public void FilterError_RefusesAValueThatIsNotAnId(string filter)
    {
        // Act
        var error = CaseMap.FilterError(filter);

        // Assert
        error.Should().Contain("is not an id");
    }

    [Theory]
    [InlineData("watchers=0196a8d5-0000-7000-8000-0000000000aa", new[] { 1, 2 })]
    [InlineData("watchers=0196a8d5-0000-7000-8000-0000000000cc", new[] { 2 })]
    [InlineData("watchers!=", new[] { 1, 2 })]
    public void ApplyFilter_MatchesARowHoldingAnyOfACollectionsValues(string filter, int[] expected)
    {
        // Act
        var ids = FilterCases(filter);

        // Assert
        ids.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("watchers=")]
    [InlineData("stage=Active|watchers=")]
    [InlineData("(watchers= ,stage=Draft)")]
    public void FilterError_RefusesComparingACollectionWithBlank(string filter)
    {
        // Act
        var error = CaseMap.FilterError(filter);

        // Assert
        error.Should().Contain("compares watchers with blank");
    }

    [Theory]
    [InlineData("watchers!=0196a8d5-0000-7000-8000-0000000000aa", "!=")]
    [InlineData("stage=Draft,watchers!*0196", "!*")]
    [InlineData("(watchers^0196|stage=Active)", "^")]
    [InlineData("watchers>0196a8d5-0000-7000-8000-0000000000aa", ">")]
    public void FilterError_RefusesAComparisonACollectionCannotMean(string filter, string op)
    {
        // Act — Gridify reads each per item, so `watchers!=ada` would match a row holding Ada and Alan
        var error = CaseMap.FilterError(filter);

        // Assert
        error.Should().Contain($"compares watchers with '{op}'");
    }

    [Theory]
    [InlineData("watchers=0196a8d5-0000-7000-8000-0000000000aa")]
    [InlineData("watchers!=")]
    [InlineData("(watchers=0196a8d5-0000-7000-8000-0000000000aa|watchers=0196a8d5-0000-7000-8000-0000000000cc),lead!=")]
    public void FilterError_AllowsTheComparisonsACollectionMeans(string filter)
    {
        // Act
        var error = CaseMap.FilterError(filter);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void FilterError_AllowsAnyComparisonOnAFieldThatIsNotACollection()
    {
        // Act
        var error = CaseMap.FilterError($"owner!={Ada.Id},lead!=,stage!=Draft");

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void FilterError_AllowsComparingAFieldWithBlank()
    {
        // Act
        var error = CaseMap.FilterError("lead=");

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void SortError_RefusesSortingByACollection()
    {
        // Act
        var error = CaseMap.SortError("stage asc, watchers desc");

        // Assert
        error.Should().Contain("names watchers, which this list filters by but cannot sort by");
    }

    [Fact]
    public async Task ReadOptions_ListsEachValueOnceInLabelOrder()
    {
        // Act
        var options = await CaseMap.ReadOptions(
            Cases.AsDbSet(), "watchers", null, 10, TestContext.Current.CancellationToken);

        // Assert
        options.Options.Should().Equal(
            new ListFilterOptionDto { Value = "0196a8d5-0000-7000-8000-0000000000aa", Label = "Ada Lovelace" },
            new ListFilterOptionDto { Value = "0196a8d5-0000-7000-8000-0000000000cc", Label = "Alan Turing" });
        options.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task ReadOptions_OrdersByRankBeforeLabel()
    {
        // Act
        var options = await CaseMap.ReadOptions(
            Cases.AsDbSet(), "stage", null, 10, TestContext.Current.CancellationToken);

        // Assert — values are codes, which the field's filter reads back
        options.Options.Select(o => o.Value).Should().Equal("Closed", "Active", "Draft");
    }

    [Fact]
    public async Task ReadOptions_KeepsOnlyLabelsContainingTheSearch()
    {
        // Act
        var options = await CaseMap.ReadOptions(
            Cases.AsDbSet(), "owner", " Hop ", 10, TestContext.Current.CancellationToken);

        // Assert
        options.Options.Should().ContainSingle().Which.Label.Should().Be("Grace Hopper");
    }

    [Fact]
    public async Task ReadOptions_StopsAtTheLimitAndSaysThereAreMore()
    {
        // Act
        var options = await CaseMap.ReadOptions(
            Cases.AsDbSet(), "owner", null, 1, TestContext.Current.CancellationToken);

        // Assert
        options.Options.Should().ContainSingle().Which.Label.Should().Be("Ada Lovelace");
        options.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task ReadOptions_SaysThereAreNoMoreWhenTheLimitIsExactlyReached()
    {
        // Act
        var options = await CaseMap.ReadOptions(
            Cases.AsDbSet(), "owner", null, 2, TestContext.Current.CancellationToken);

        // Assert
        options.Options.Should().HaveCount(2);
        options.HasMore.Should().BeFalse();
    }

    [Theory]
    [InlineData("owner", true)]
    [InlineData("OWNER", true)]
    [InlineData("lead", false)]
    [InlineData(null, false)]
    public void OptionsError_AcceptsOnlyAFieldWithOptions(string? field, bool accepted)
    {
        // Act
        var error = CaseMap.OptionsError(field);

        // Assert
        if (accepted)
            error.Should().BeNull();
        else
            error.Should().Contain("owner, stage, watchers");
    }

    [Fact]
    public void Options_RefusesAFieldTheMapDoesNotFilterBy()
    {
        // Act
        var act = () => ListFieldMap<Case>.Create("id", c => c.Id)
            .Map("id", c => c.Id)
            .Options("owner", cases => cases.Select(c => new ListFilterOptionRow<Guid> { Value = c.Owner.Id, Label = c.Owner.Name, Rank = 0 }));

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*'owner' is not a field of this map*");
    }
}
