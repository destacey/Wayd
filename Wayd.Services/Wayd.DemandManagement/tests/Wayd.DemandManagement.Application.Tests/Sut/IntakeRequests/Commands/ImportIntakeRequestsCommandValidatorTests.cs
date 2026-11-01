using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Imports.Commands;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.Tests.Shared;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class ImportIntakeRequestsCommandValidatorTests
{
    private readonly ImportIntakeRequestsCommandValidator _validator =
        new(new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 10, 1, 8, 0, 0))));

    private static SubmittedImportRow<ImportIntakeRequestDto> Row(string importId, ImportIntakeRequestOriginDto? origin) =>
        new(importId, new ImportIntakeRequestDto(
            "Export invoices to the new ledger", "Finance needs invoices in the new ledger.", null,
            Guid.CreateVersion7(), null, Guid.CreateVersion7(), null, Instant.FromUtc(2026, 6, 1, 9, 0, 0),
            null, origin, null, null, null, null, null, []));

    [Fact]
    public void Validate_RefusesAFileInWhichTwoRowsShareAnOrigin()
    {
        // Arrange — rows in different chunks never see each other's origins during a check run
        var command = new ImportIntakeRequestsCommand(
        [
            Row("r1", new ImportIntakeRequestOriginDto("ServiceNow", "RITM1", null)),
            Row("r2", new ImportIntakeRequestOriginDto("servicenow ", "ritm1", null)),
        ]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("OriginExternalId"));
    }

    [Fact]
    public void Validate_AcceptsRowsWithDistinctOriginsOrNone()
    {
        // Arrange
        var command = new ImportIntakeRequestsCommand(
        [
            Row("r1", new ImportIntakeRequestOriginDto("ServiceNow", "RITM1", null)),
            Row("r2", new ImportIntakeRequestOriginDto("ServiceNow", "RITM2", null)),
            Row("r3", null),
            Row("r4", null),
        ]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
