using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.Tests.Shared;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Dtos;

/// <summary>
/// The rules a dependency row is held to before the import runs.
/// </summary>
public sealed class ImportProductDependencyDtoValidatorTests
{
    private static readonly LocalDate Today = new(2026, 4, 1);

    // 2 April in UTC, still Today in the system zone, which is the day a blank start means.
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 2, 5, 0);

    private readonly ImportProductDependencyDtoValidator _sut =
        new(new TestingSystemCalendar(new TestingDateTimeProvider(new FakeClock(Now))));

    private static ImportProductDependencyDto Row(LocalDate? startsOn, LocalDate? endsOn) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), DependencyStrength.Hard, null, null, startsOn, endsOn);

    [Fact]
    public async Task Validate_ShouldRejectAnEndBeforeTheStart()
    {
        // Arrange
        var row = Row(Today.PlusDays(-10), Today.PlusDays(-20));

        // Act
        var result = await _sut.ValidateAsync(row, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldRejectAnEndBeforeTodayWhenTheStartIsBlank()
    {
        // Arrange — a blank start is today, so this row would end before it began
        var row = Row(null, Today.PlusDays(-1));

        // Act
        var result = await _sut.ValidateAsync(row, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("A dependency cannot end before it started. A blank StartsOn means today.");
    }

    [Fact]
    public async Task Validate_ShouldAcceptAnEndOnTheStartDay()
    {
        // Arrange — the last day counts, so a link can start and end on the same day; a blank start is today in
        // the system zone, not the UTC day after it
        var dated = Row(Today.PlusDays(-5), Today.PlusDays(-5));
        var today = Row(null, Today);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var datedResult = await _sut.ValidateAsync(dated, cancellationToken);
        var todayResult = await _sut.ValidateAsync(today, cancellationToken);

        // Assert
        datedResult.IsValid.Should().BeTrue();
        todayResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ShouldAcceptAnOpenLink()
    {
        // Arrange
        var row = Row(null, null);

        // Act
        var result = await _sut.ValidateAsync(row, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
