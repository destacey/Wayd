using FluentAssertions;
using NodaTime;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Dtos;

/// <summary>
/// Which transition dates a portfolio row may carry. There is no closing date here: an import cannot
/// close a portfolio, so that one belongs to the finalize row instead.
/// </summary>
public sealed class ImportProjectPortfolioDtoValidatorTests
{
    private static readonly LocalDate _created = new(2024, 3, 4);
    private static readonly LocalDate _activated = new(2024, 7, 8);

    private readonly ImportProjectPortfolioDtoValidator _sut = new();

    [Theory]
    [InlineData(ProjectPortfolioStatus.Active)]
    [InlineData(ProjectPortfolioStatus.OnHold)]
    [InlineData(ProjectPortfolioStatus.Closed)]
    [InlineData(ProjectPortfolioStatus.Archived)]
    public void Validate_RejectsAPortfolioPastProposedWithNoActivationDate(ProjectPortfolioStatus status)
    {
        // Arrange — every one of these statuses was reached by activating, so the date is what the
        // portfolio's own date range is built from
        var row = Row(status) with { ActivatedOn = null };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ImportProjectPortfolioDto.ActivatedOn));
    }

    [Fact]
    public void Validate_RejectsAnActivationDateOnAProposedPortfolio()
    {
        // Arrange
        var row = Row(ProjectPortfolioStatus.Proposed) with { ActivatedOn = _activated };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsAnActivationBeforeThePortfolioWasCreated()
    {
        // Arrange
        var row = Row(ProjectPortfolioStatus.Active) with { ActivatedOn = _created.PlusDays(-1) };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(ProjectPortfolioStatus.Proposed)]
    [InlineData(ProjectPortfolioStatus.Active)]
    [InlineData(ProjectPortfolioStatus.OnHold)]
    public void Validate_AcceptsARowCarryingTheDatesItsStatusCallsFor(ProjectPortfolioStatus status)
    {
        // Arrange
        var row = Row(status);

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    private static ImportProjectPortfolioDto Row(ProjectPortfolioStatus status) =>
        new(
            "Growth",
            "Growth portfolio",
            status,
            _created,
            status is ProjectPortfolioStatus.Proposed ? null : _activated,
            [],
            [],
            []);
}
