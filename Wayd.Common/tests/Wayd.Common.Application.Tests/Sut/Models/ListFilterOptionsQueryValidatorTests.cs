using FluentAssertions;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Tests.Sut.Models;

public sealed class ListFilterOptionsQueryValidatorTests
{
    public sealed record Row(int Id, string Name, string Note);

    public sealed record Query(string Field, string? Search = null, int Limit = 100) : IListFilterOptionsQuery;

    private static readonly ListFieldMap<Row> Map =
        ListFieldMap<Row>.Create("name", r => r.Id)
            .Map("name", r => r.Name)
            .Map("note", r => r.Note)
            .Options("name", rows => rows.Select(r => new ListFilterOptionRow<string> { Value = r.Name, Label = r.Name, Rank = 0 }));

    private sealed class Validator() : ListFilterOptionsQueryValidator<Query, Row>(Map);

    [Fact]
    public void Validate_AcceptsAFieldWithOptionsWithinBounds()
    {
        // Act
        var result = new Validator().Validate(new Query("name", new string('a', ListFilterOptionsQueryValidator<Query, Row>.MaxSearchLength), ListFieldMap<Row>.MaxOptionsLimit));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("note")]
    [InlineData("missing")]
    public void Validate_RefusesAFieldWithoutOptions(string field)
    {
        // Act
        var result = new Validator().Validate(new Query(field));

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(IListFilterOptionsQuery.Field));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ListFieldMap<Row>.MaxOptionsLimit + 1)]
    public void Validate_RefusesALimitOutOfBounds(int limit)
    {
        // Act
        var result = new Validator().Validate(new Query("name", Limit: limit));

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(IListFilterOptionsQuery.Limit));
    }

    [Fact]
    public void Validate_RefusesASearchLongerThanTheMaximum()
    {
        // Act
        var result = new Validator().Validate(
            new Query("name", new string('a', ListFilterOptionsQueryValidator<Query, Row>.MaxSearchLength + 1)));

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(IListFilterOptionsQuery.Search));
    }
}
