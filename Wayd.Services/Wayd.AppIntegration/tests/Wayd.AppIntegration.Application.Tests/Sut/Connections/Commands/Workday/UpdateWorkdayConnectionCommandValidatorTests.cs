using FluentAssertions;
using Wayd.AppIntegration.Application.Connections.Commands.Workday;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Workday;

public sealed class UpdateWorkdayConnectionCommandValidatorTests
{
    private readonly UpdateWorkdayConnectionCommandValidator _sut = new();

    [Fact]
    public async Task Validate_Passes_WhenTheTenantFitsItsOutsideSystem()
    {
        // Arrange
        var command = CreateCommand(new string('t', ExternalSystem.SystemIdMaxLength));

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_Fails_WhenTheTenantIsTooLongForItsOutsideSystem()
    {
        // Arrange
        var command = CreateCommand(new string('t', ExternalSystem.SystemIdMaxLength + 1));

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "WsdlUrl")
            .Which.ErrorMessage.Should().Contain($"{ExternalSystem.SystemIdMaxLength} characters");
    }

    private static UpdateWorkdayConnectionCommand CreateCommand(string tenant) => new(
        Guid.NewGuid(),
        "Workday",
        null,
        $"https://wd.acme.example/ccx/service/{tenant}/Staffing/v46.1",
        "isu@acme_corp",
        "password",
        WorkerKey: WorkdayWorkerKey.EmployeeId,
        IncludeInactive: false,
        MatchBy: EmployeeMatchProperty.Email,
        UseUserIdAsEmailFallback: false,
        UsePreferredName: false,
        NormalizeNameCasing: true,
        DepartmentOrganizationTypeId: null,
        OrgExclusions: null);
}
