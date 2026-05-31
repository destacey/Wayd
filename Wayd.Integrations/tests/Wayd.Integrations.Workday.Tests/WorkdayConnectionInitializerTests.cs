using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Workday.Soap;
using Wayd.Integrations.Workday.Tests.Infrastructure;
using Xunit;

namespace Wayd.Integrations.Workday.Tests;

public class WorkdayConnectionInitializerTests
{
    private static WorkdayConnectionCredentials BuildCredentials(
        WorkdayWorkerKey key = WorkdayWorkerKey.Wid,
        bool useUserIdAsEmailFallback = false,
        bool usePreferredName = false) => new(
        SoapEndpoint: "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1",
        TenantAlias: "acme_corp1",
        WsdlVersion: "v46.1",
        IsuUsername: "wayd_isu@acme_corp1",
        IsuPassword: "secret",
        WorkerKey: key,
        IncludeInactive: false,
        IncrementalUpdatedFrom: null,
        UseUserIdAsEmailFallback: useUserIdAsEmailFallback,
        UsePreferredName: usePreferredName);

    private static (WorkdayConnectionInitializer initializer, FakeHttpMessageHandler handler) BuildInitializer()
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var client = new WorkdayStaffingClient(httpClient, NullLogger<WorkdayStaffingClient>.Instance);
        var initializer = new WorkdayConnectionInitializer(client, NullLogger<WorkdayConnectionInitializer>.Instance);
        return (initializer, handler);
    }

    [Fact]
    public async Task Initialize_healthyTenant_returnsValid()
    {
        var (initializer, handler) = BuildInitializer();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await initializer.Initialize(BuildCredentials(), CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.WorkersProbed.Should().Be(2);
        result.MissingRequiredFields.Should().BeEmpty();
        result.AuthError.Should().BeNull();
    }

    [Fact]
    public async Task Initialize_missingEmailField_reportsAsRequiredFieldMissing()
    {
        var (initializer, handler) = BuildInitializer();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-missing-email.xml"));

        var result = await initializer.Initialize(BuildCredentials(), CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.WorkersProbed.Should().Be(2);
        // The label leads with "Work Email"; we use a prefix match so the helpful guidance
        // suffix (ISSG domain name + fallback hint) can evolve without churning this test.
        result.MissingRequiredFields.Should().Contain(s => s.StartsWith("Work Email"));
        result.AuthError.Should().BeNull();
    }

    [Fact]
    public async Task Initialize_authFault_reportsAuthError()
    {
        var (initializer, handler) = BuildInitializer();
        handler.EnqueueFault(File.ReadAllText("Fixtures/auth-fault.xml"));

        var result = await initializer.Initialize(BuildCredentials(), CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.AuthError.Should().NotBeNull();
        result.AuthError.Should().Contain("Authentication failed");
    }

    [Fact]
    public async Task Initialize_workerKeyEmployeeId_requiresEmployeeIdField()
    {
        var (initializer, handler) = BuildInitializer();
        // The missing-email fixture also lacks Employee_ID on Worker_Reference, so when the admin
        // selects Employee_ID as the upsert key, the initializer should flag it as missing in
        // addition to Work Email.
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-missing-email.xml"));

        var result = await initializer.Initialize(BuildCredentials(WorkdayWorkerKey.EmployeeId), CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.MissingRequiredFields.Should().Contain("Employee ID");
    }

    [Fact]
    public async Task Initialize_userIdEmailFallback_on_acceptsEmailShapedUserId()
    {
        var (initializer, handler) = BuildInitializer();
        // No Contact_Data at all, but every worker has an email-shaped User_ID. With the fallback
        // toggle on, the probe should consider Work Email satisfied.
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-userid-email.xml"));

        var result = await initializer.Initialize(
            BuildCredentials(useUserIdAsEmailFallback: true),
            CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.MissingRequiredFields.Should().NotContain(s => s.StartsWith("Work Email"));
        result.Warnings.Should().Contain(w => w.Contains("User_ID"));
    }

    [Fact]
    public async Task Initialize_userIdEmailFallback_off_stillFlagsWorkEmailMissing()
    {
        var (initializer, handler) = BuildInitializer();
        // Same fixture, but the toggle is off, so User_ID doesn't count and Work Email is missing.
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-userid-email.xml"));

        var result = await initializer.Initialize(
            BuildCredentials(useUserIdAsEmailFallback: false),
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.MissingRequiredFields.Should().Contain(s => s.StartsWith("Work Email"));
    }

    [Fact]
    public async Task Initialize_userIdEmailFallback_on_rejectsNonEmailShapedUserId()
    {
        var (initializer, handler) = BuildInitializer();
        // The User_ID values look like usernames, not emails ("EMP-1234", "casey-park"). Even
        // with the fallback on, the probe should still surface Work Email as missing because
        // the fallback only accepts values that parse as email addresses.
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-userid-not-email.xml"));

        var result = await initializer.Initialize(
            BuildCredentials(useUserIdAsEmailFallback: true),
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.MissingRequiredFields.Should().Contain(s => s.StartsWith("Work Email"));
    }
}
