using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

namespace Wayd.Web.Api.Tests.Sut.Models.DemandManagement.IntakeRequests;

public sealed class ImportIntakeRequestRequestValidatorTests
{
    private readonly ImportIntakeRequestRequestValidator _validator = new();

    private static ImportIntakeRequestRequest Row(string? originUrl) => new()
    {
        Title = "Export invoices to the new ledger",
        Description = "Finance needs invoices in the new ledger.",
        DeskId = Guid.CreateVersion7(),
        RequesterId = Guid.CreateVersion7(),
        OriginSourceSystem = "ServiceNow",
        OriginExternalId = "RITM0001042",
        OriginUrl = originUrl,
    };

    [Theory]
    [InlineData("https://servicenow.example.com/request/RITM0001042")]
    [InlineData(null)]
    public void Validate_AcceptsAnOriginUrlThatIsAWebAddressOrNone(string? originUrl)
    {
        // Act
        var result = _validator.TestValidate(Row(originUrl));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("servicenow.example.com/request/RITM0001042")]
    public void Validate_RefusesAnOriginUrlThatIsNotAWebAddress(string originUrl)
    {
        // Act
        var result = _validator.TestValidate(Row(originUrl));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.OriginUrl);
    }
}
