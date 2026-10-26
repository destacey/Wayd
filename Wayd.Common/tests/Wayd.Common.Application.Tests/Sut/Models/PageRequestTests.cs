using FluentAssertions;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Tests.Sut.Models;

public sealed class PageRequestTests
{
    private sealed record PagedTestQuery(PageRequest Paging) : IPagedQuery;

    private sealed class PagedTestQueryValidator : PagedQueryValidator<PagedTestQuery>;

    private sealed class LargerPageTestQueryValidator() : PagedQueryValidator<PagedTestQuery>(1000);

    private readonly PagedTestQueryValidator _validator = new();

    [Fact]
    public void New_ShouldReadTheFirstPageAtTheSharedDefaultSize()
    {
        // Act
        var paging = new PageRequest();

        // Assert
        paging.Page.Should().Be(1);
        paging.PageSize.Should().Be(50);
    }

    [Theory]
    [InlineData(1, 25, 0)]
    [InlineData(2, 10, 10)]
    [InlineData(3, 25, 50)]
    public void Skip_ShouldCountTheItemsOnEarlierPages(int page, int pageSize, int expected)
    {
        // Act
        var skip = new PageRequest(page, pageSize).Skip;

        // Assert
        skip.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 500)]
    [InlineData(40, 25)]
    public void Validate_ShouldAcceptAPageWithinTheBounds(int page, int pageSize)
    {
        // Act
        var result = _validator.Validate(new PagedTestQuery(new PageRequest(page, pageSize)));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldRefuseAPageBeforeTheFirst_NamingThePageParameter(int page)
    {
        // Act
        var result = _validator.Validate(new PagedTestQuery(new PageRequest(page)));

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Page");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Validate_ShouldRefuseAPageSizeOutsideTheBounds_NamingThePageSizeParameter(int pageSize)
    {
        // Act
        var result = _validator.Validate(new PagedTestQuery(new PageRequest(PageSize: pageSize)));

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("PageSize");
    }

    [Fact]
    public void Validate_ShouldRefuseAQueryWithNoPage()
    {
        // Act
        var result = _validator.Validate(new PagedTestQuery(null!));

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(IPagedQuery.Paging));
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Validate_ShouldBoundAPageSizeByTheListsOwnMaximum_WhenItDeclaresOne(int pageSize, bool valid)
    {
        // Arrange
        var validator = new LargerPageTestQueryValidator();

        // Act
        var result = validator.Validate(new PagedTestQuery(new PageRequest(PageSize: pageSize)));

        // Assert
        validator.MaxPageSize.Should().Be(1000);
        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validate_ShouldRefuseAPageWhoseOffsetWouldOverflow_NamingThePageParameter()
    {
        // Act
        var result = _validator.Validate(new PagedTestQuery(new PageRequest(int.MaxValue, 500)));

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Page");
    }
}
