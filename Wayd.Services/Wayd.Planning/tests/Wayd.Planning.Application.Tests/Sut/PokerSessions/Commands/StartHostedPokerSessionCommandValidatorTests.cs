using FluentValidation.TestHelper;
using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PokerSessions.Commands;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public sealed class StartHostedPokerSessionCommandValidatorTests
{
    private readonly StartHostedPokerSessionCommandValidator _validator = new();

    private static PokerRoundSubjectInput ARequest(string title = "Export invoices") =>
        new(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), "42", title, null);

    private static StartHostedPokerSessionCommand Command(params PokerRoundSubjectInput[] subjects) =>
        new("Desk Refinement", PokerSessionHostKind.IntakeDesk, Guid.NewGuid(), Guid.NewGuid(), subjects);

    [Fact]
    public void WithNoRecords_Passes()
    {
        // Act
        var result = _validator.TestValidate(Command());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BlankName_Fails()
    {
        // Act
        var result = _validator.TestValidate(Command() with { Name = " " });

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void EmptyHostOrVersion_Fails()
    {
        // Act
        var result = _validator.TestValidate(Command() with { HostId = Guid.Empty, EstimationScaleVersionId = Guid.Empty });

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.HostId);
        result.ShouldHaveValidationErrorFor(c => c.EstimationScaleVersionId);
    }

    [Fact]
    public void TooManyRecords_Fails()
    {
        // Arrange
        var subjects = Enumerable.Range(0, PokerRoundSubjectInputValidator.MaxSubjectsPerRequest + 1).Select(_ => ARequest()).ToArray();

        // Act
        var result = _validator.TestValidate(Command(subjects));

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Subjects);
    }

    [Fact]
    public void ARecordWithNoTitle_Fails()
    {
        // Act
        var result = _validator.TestValidate(Command(ARequest(title: "")));

        // Assert
        result.ShouldHaveValidationErrorFor("Subjects[0].Title");
    }
}
