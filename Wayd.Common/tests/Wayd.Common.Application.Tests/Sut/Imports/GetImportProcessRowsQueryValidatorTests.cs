using FluentAssertions;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class GetImportProcessRowsQueryValidatorTests
{
    private readonly GetImportProcessRowsQueryValidator _validator = new();

    private static GetImportProcessRowsQuery Query(string? filter = null, string? sort = null) =>
        new(Guid.NewGuid(), new PageRequest(), Filter: filter, Sort: sort);

    [Fact]
    public void Validate_ShouldAcceptAFilterAndSortOverMappedFields()
    {
        // Arrange
        var query = Query("(status=Failed|status=Cancelled),error=*duplicate,attemptedOn>=2026-03-01T00:00:00Z", "status asc, rowNumber desc");

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldReject_AFilterOnAnUnmappedField_NamedFilter()
    {
        // Arrange — the record link is resolved after the page is read, so SQL cannot filter on it
        var query = Query("createdRecord=*x");

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(IFilteredPagedQuery.Filter));
    }

    [Fact]
    public void Validate_ShouldReject_ASortOnAnUnmappedField_NamedSort()
    {
        // Arrange
        var query = Query(sort: "payload desc");

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(IFilteredPagedQuery.Sort));
    }

    [Fact]
    public void Validate_ShouldReject_AStatusThatIsNotACode()
    {
        // Arrange
        var query = Query("status=Exploded");

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldKeepThePagingBounds()
    {
        // Arrange
        var query = new GetImportProcessRowsQuery(Guid.NewGuid(), new PageRequest(1, PageSizes.Max + 1));

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(PageRequest.PageSize));
    }
}
