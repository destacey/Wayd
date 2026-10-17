using FluentValidation;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Common.Application.Validation;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Common.Application.Tests.Sut.Validators;

public class ReportingCadenceValidatorExtensionTests
{
    private static readonly LocalDate Anchor = new(2026, 10, 5);

    private readonly CadenceValidator _sut = new();

    [Theory]
    [InlineData(Methodology.Kanban, null, false)]
    [InlineData(Methodology.Kanban, 2, true)]
    [InlineData(Methodology.Scrum, null, false)]
    public void Validate_WithAValidCadenceOrNone_HasNoErrors(Methodology methodology, int? weeks, bool hasAnchor)
    {
        // Arrange
        var model = new CadenceModel(methodology, weeks, hasAnchor ? Anchor : null);

        // Act
        var result = _sut.TestValidate(model);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ScrumWithACadence_HasAWeeksError()
    {
        // Arrange
        var model = new CadenceModel(Methodology.Scrum, 2, Anchor);

        // Act
        var result = _sut.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Weeks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Validate_WithALengthOutOfRange_HasAWeeksError(int weeks)
    {
        // Arrange
        var model = new CadenceModel(Methodology.Kanban, weeks, Anchor);

        // Act
        var result = _sut.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Weeks);
    }

    [Fact]
    public void Validate_WithALengthAndNoAnchor_HasAnAnchorError()
    {
        // Arrange
        var model = new CadenceModel(Methodology.Kanban, 2, null);

        // Act
        var result = _sut.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Anchor);
    }

    [Fact]
    public void Validate_WithAnAnchorAndNoLength_HasAnAnchorError()
    {
        // Arrange
        var model = new CadenceModel(Methodology.Kanban, null, Anchor);

        // Act
        var result = _sut.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(m => m.Anchor);
    }

    public sealed record CadenceModel(Methodology Methodology, int? Weeks, LocalDate? Anchor);

    private sealed class CadenceValidator : AbstractValidator<CadenceModel>
    {
        public CadenceValidator()
        {
            this.RulesForReportingCadence(m => m.Methodology, m => m.Weeks, m => m.Anchor);
        }
    }
}
