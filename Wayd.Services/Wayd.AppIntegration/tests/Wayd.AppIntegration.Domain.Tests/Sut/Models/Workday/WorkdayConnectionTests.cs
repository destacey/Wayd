using FluentAssertions;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Domain.Tests.Sut.Models.Workday;

/// <summary>
/// Holds the rule that the stored ISU password is only sent to the host, tenant and user it was entered for:
/// an edit that changes any of them must supply the password again.
/// </summary>
public sealed class WorkdayConnectionTests
{
    private const string OriginalUrl = "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1?wsdl";
    private const string OriginalUsername = "wayd_isu@acme_corp1";
    private const string OriginalPassword = "original-password";

    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString());

    private readonly TestingDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 02, 10, 12, 0, 0));

    [Theory]
    [InlineData("https://attacker.example.com/ccx/service/acme_corp1/Staffing/v46.1", OriginalUsername)]
    [InlineData("https://wd3-impl-services1.workday.com:8443/ccx/service/acme_corp1/Staffing/v46.1", OriginalUsername)]
    [InlineData("https://wd3-impl-services1.workday.com/ccx/service/other_tenant/Staffing/v46.1", OriginalUsername)]
    [InlineData(OriginalUrl, "someone_else@acme_corp1")]
    public void Update_RefusesToKeepThePassword_WhenHostTenantOrUserChanges(string wsdlUrl, string username)
    {
        // Arrange
        var connection = Create();
        var eventsBefore = connection.DomainEvents.Count;

        // Act
        var result = Update(connection, wsdlUrl, username, isuPassword: null, name: "Renamed");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Connection.CredentialsRequiredMessage("ISU password"));
        connection.Name.Should().Be("People");
        connection.IsValidConfiguration.Should().BeTrue();
        connection.Configuration.WsdlUrl.Should().Be(OriginalUrl);
        connection.Configuration.IsuUsername.Should().Be(OriginalUsername);
        connection.Configuration.IsuPassword.Should().Be(OriginalPassword);
        connection.DomainEvents.Should().HaveCount(eventsBefore);
    }

    [Fact]
    public void Update_MovesToANewHost_WhenThePasswordIsEnteredAgain()
    {
        // Arrange
        var connection = Create();
        const string newUrl = "https://wd5-services1.myworkday.com/ccx/service/acme_corp1/Staffing/v46.1";

        // Act
        var result = Update(connection, newUrl, OriginalUsername, OriginalPassword);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.ServiceHost.Should().Be("wd5-services1.myworkday.com");
        connection.Configuration.IsuPassword.Should().Be(OriginalPassword);
    }

    [Theory]
    [InlineData("https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v47.0", OriginalUsername)]
    [InlineData("https://WD3-IMPL-SERVICES1.workday.com/ccx/service/ACME_CORP1/Staffing/v46.1", "WAYD_ISU@acme_corp1")]
    public void Update_KeepsThePassword_WhenOnlyTheVersionOrCaseChanges(string wsdlUrl, string username)
    {
        // Arrange
        var connection = Create();

        // Act
        var result = Update(connection, wsdlUrl, username, isuPassword: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.WsdlUrl.Should().Be(wsdlUrl);
        connection.Configuration.IsuPassword.Should().Be(OriginalPassword);
    }

    private WorkdayConnection Create() =>
        WorkdayConnection.Create(
            "People",
            null,
            new WorkdayConnectionConfiguration(OriginalUrl, OriginalUsername, OriginalPassword),
            true,
            Actor,
            _dateTimeProvider.Now);

    private CSharpFunctionalExtensions.Result Update(WorkdayConnection connection, string wsdlUrl, string username, string? isuPassword, string name = "People") =>
        connection.Update(
            name,
            null,
            wsdlUrl,
            username,
            isuPassword,
            WorkdayWorkerKey.EmployeeId,
            includeInactive: false,
            EmployeeMatchProperty.Email,
            useUserIdAsEmailFallback: false,
            usePreferredName: false,
            normalizeNameCasing: true,
            WorkdayConnectionConfiguration.SupervisoryOrgTypeId,
            orgExclusions: null,
            configurationIsValid: false,
            Actor,
            _dateTimeProvider.Now);
}
