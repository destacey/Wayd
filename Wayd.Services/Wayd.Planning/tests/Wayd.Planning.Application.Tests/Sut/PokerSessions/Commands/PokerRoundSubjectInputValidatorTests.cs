using FluentValidation.TestHelper;
using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public sealed class PokerRoundSubjectInputValidatorTests
{
    private readonly PokerRoundSubjectInputValidator _validator = new();

    private static PokerRoundSubjectInput ARequest(string displayKey = "42", string title = "Export invoices", string? description = null) =>
        new(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), displayKey, title, description);

    [Fact]
    public void ARecord_Passes()
    {
        // Act
        var result = _validator.TestValidate(ARequest());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ALongDescription_Passes()
    {
        // Arrange — the subject keeps its start rather than refusing the record
        var input = ARequest(description: new string('a', PokerRound.DescriptionMaxLength * 2));

        // Act
        var result = _validator.TestValidate(input);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BlankTitle_Fails()
    {
        // Act
        var result = _validator.TestValidate(ARequest(title: ""));

        // Assert
        result.ShouldHaveValidationErrorFor(s => s.Title);
    }

    [Fact]
    public void TitleTooLong_Fails()
    {
        // Act
        var result = _validator.TestValidate(ARequest(title: new string('a', PokerRound.LabelMaxLength + 1)));

        // Assert
        result.ShouldHaveValidationErrorFor(s => s.Title);
    }

    [Fact]
    public void DisplayKeyTooLong_Fails()
    {
        // Act
        var result = _validator.TestValidate(ARequest(displayKey: new string('1', PokerRoundSubject.DisplayKeyMaxLength + 1)));

        // Assert
        result.ShouldHaveValidationErrorFor(s => s.DisplayKey);
    }

    [Fact]
    public void EmptyId_Fails()
    {
        // Act
        var result = _validator.TestValidate(ARequest() with { Id = Guid.Empty });

        // Assert
        result.ShouldHaveValidationErrorFor(s => s.Id);
    }
}
