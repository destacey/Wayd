using FluentAssertions;
using Wayd.ProductManagement.Application.Incidents.Commands;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Commands;

public sealed class CreateIncidentCommandValidatorTests
{
    [Fact]
    public void Validate_ShouldRefuse_WhenTheProductListIsNull()
    {
        // Arrange — a request that sends "productIds": null deserializes to a null list
        var command = new CreateIncidentCommand(
            "Checkout unavailable", null, null, null, Guid.CreateVersion7(), NodaTime.Instant.FromUtc(2026, 4, 2, 5, 0), null, null,
            null!, Guid.CreateVersion7());

        // Act
        var result = new CreateIncidentCommandValidator().Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateIncidentCommand.ProductIds));
    }
}
