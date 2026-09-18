using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Dtos;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Dtos;

/// <summary>
/// The rules a dependency row is held to before the import runs.
/// </summary>
public sealed class ImportProductDependencyDtoValidatorTests
{
    private static readonly LocalDate Today = new(2026, 4, 1);

    private readonly ImportProductDependencyDtoValidator _sut;

    public ImportProductDependencyDtoValidatorTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Today).Returns(Today);

        _sut = new ImportProductDependencyDtoValidator(dateTimeProvider.Object);
    }

    private static ImportProductDependencyDto Row(LocalDate? startsOn, LocalDate? endsOn) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), DependencyStrength.Hard, null, startsOn, endsOn);

    [Fact]
    public void Validate_ShouldRejectAnEndBeforeTheStart()
    {
        // Arrange
        var row = Row(Today.PlusDays(-10), Today.PlusDays(-20));

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldRejectAnEndBeforeTodayWhenTheStartIsBlank()
    {
        // Arrange — a blank start is today, so this row would end before it began
        var row = Row(null, Today.PlusDays(-1));

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("A dependency cannot end before it started. A blank StartsOn means today.");
    }

    [Fact]
    public void Validate_ShouldAcceptAnEndOnTheStartDay()
    {
        // Arrange — the last day counts, so a link can start and end on the same day
        var dated = Row(Today.PlusDays(-5), Today.PlusDays(-5));
        var today = Row(null, Today);

        // Act
        var datedResult = _sut.Validate(dated);
        var todayResult = _sut.Validate(today);

        // Assert
        datedResult.IsValid.Should().BeTrue();
        todayResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldAcceptAnOpenLink()
    {
        // Arrange
        var row = Row(null, null);

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
