using FluentAssertions;
using NodaTime;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Dtos;

/// <summary>
/// Which transition dates a program row may carry. There is no closing date here: an import cannot
/// complete or cancel a program, so that one belongs to the finalize row instead.
/// </summary>
public sealed class ImportProgramDtoValidatorTests
{
    private static readonly LocalDate _created = new(2024, 3, 4);
    private static readonly LocalDate _activated = new(2024, 7, 8);

    private readonly ImportProgramDtoValidator _sut = new();

    [Theory]
    [InlineData(ProgramStatus.Active)]
    [InlineData(ProgramStatus.Completed)]
    public void Validate_RejectsAProgramPastProposedWithNoActivationDate(ProgramStatus status)
    {
        // Arrange
        var row = Row(status) with { ActivatedOn = null };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ImportProgramDto.ActivatedOn));
    }

    [Fact]
    public void Validate_RejectsAnActivationDateOnAProposedProgram()
    {
        // Arrange
        var row = Row(ProgramStatus.Proposed) with { ActivatedOn = _activated };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsACanceledProgramEitherWayRoundOnActivation()
    {
        // Arrange — Canceled is the one status the row alone cannot settle: a program can be canceled
        // before it ever started or part-way through, so the date is optional here and nowhere else
        var never = Row(ProgramStatus.Canceled) with { ActivatedOn = null };
        var midFlight = Row(ProgramStatus.Canceled) with { ActivatedOn = _activated };

        // Act & Assert
        _sut.Validate(never).IsValid.Should().BeTrue();
        _sut.Validate(midFlight).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsAnActivationBeforeTheProgramWasCreated()
    {
        // Arrange
        var row = Row(ProgramStatus.Active) with { ActivatedOn = _created.PlusDays(-1) };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsAnActivatedProgramWithNoTimeline()
    {
        // Arrange — activating reads the planned range, so naming an activation without one leaves the
        // row describing a transition the domain would refuse
        var row = Row(ProgramStatus.Canceled) with { ActivatedOn = _activated, Start = null, End = null };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ImportProgramDto.Start));
    }

    [Theory]
    [InlineData(ProgramStatus.Proposed)]
    [InlineData(ProgramStatus.Active)]
    public void Validate_AcceptsARowCarryingTheDatesItsStatusCallsFor(ProgramStatus status)
    {
        // Arrange
        var row = Row(status);

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    private static ImportProgramDto Row(ProgramStatus status) =>
        new(
            "Platform",
            "Platform program",
            status,
            Guid.CreateVersion7(),
            new LocalDate(2024, 7, 1),
            new LocalDate(2025, 6, 30),
            _created,
            status is ProgramStatus.Proposed ? null : _activated,
            [],
            [],
            [],
            []);
}
