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
    private static WorkdayRequestContext BuildContext(
        WorkdayWorkerKey key = WorkdayWorkerKey.Wid,
        bool useUserIdAsEmailFallback = false,
        bool usePreferredName = false,
        bool normalizeNameCasing = false) => new(
        SoapEndpoint: "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1",
        TenantAlias: "acme_corp1",
        WsdlVersion: "v46.1",
        Credentials: new WorkdayCredentials("wayd_isu@acme_corp1", "secret"),
        WorkerKey: key,
        IncludeInactive: false,
        IncrementalUpdatedFrom: null,
        UseUserIdAsEmailFallback: useUserIdAsEmailFallback,
        UsePreferredName: usePreferredName,
        NormalizeNameCasing: normalizeNameCasing);

    private static (WorkdayConnectionInitializer initializer, FakeHttpMessageHandler handler) BuildInitializer()
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var client = new WorkdayStaffingClient(httpClient, TimeProvider.System, NullLogger<WorkdayStaffingClient>.Instance);
        var initializer = new WorkdayConnectionInitializer(client, NullLogger<WorkdayConnectionInitializer>.Instance);
        return (initializer, handler);
    }

    /// <summary>
    /// The probe issues Get_Workers followed by Get_Organizations. Most existing tests are only
    /// asserting the worker-probe outcome but the second call still happens, so we always enqueue a
    /// minimal org catalog. Tests that explicitly want to assert the catalog can use the same
    /// helper; tests that want to assert Get_Organizations failure can enqueue a fault instead.
    /// </summary>
    private static void EnqueueOrgCatalog(FakeHttpMessageHandler handler)
        => handler.EnqueueXml(File.ReadAllText("Fixtures/get-organizations-catalog.xml"));

    [Fact]
    public async Task Initialize_healthyTenant_returnsValid()
    {
        var (initializer, handler) = BuildInitializer();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));
        EnqueueOrgCatalog(handler);

        var result = await initializer.Initialize(BuildContext(), CancellationToken.None);

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
        EnqueueOrgCatalog(handler);

        var result = await initializer.Initialize(BuildContext(), CancellationToken.None);

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

        var result = await initializer.Initialize(BuildContext(), CancellationToken.None);

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
        EnqueueOrgCatalog(handler);

        var result = await initializer.Initialize(BuildContext(WorkdayWorkerKey.EmployeeId), CancellationToken.None);

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
        EnqueueOrgCatalog(handler);

        var result = await initializer.Initialize(
            BuildContext(useUserIdAsEmailFallback: true),
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
        EnqueueOrgCatalog(handler);

        var result = await initializer.Initialize(
            BuildContext(useUserIdAsEmailFallback: false),
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
        EnqueueOrgCatalog(handler);

        var result = await initializer.Initialize(
            BuildContext(useUserIdAsEmailFallback: true),
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.MissingRequiredFields.Should().Contain(s => s.StartsWith("Work Email"));
    }

    [Fact]
    public async Task Initialize_orgTypeCatalog_discoversAndGroupsByTypeId()
    {
        // The catalog fixture has 4 orgs across 3 types: two SUPERVISORY, one COST_CENTER, one
        // BUSINESS_UNIT. The probe should group them and surface counts so the admin can pick a
        // type that actually has data.
        var (initializer, handler) = BuildInitializer();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));
        EnqueueOrgCatalog(handler);

        var result = await initializer.Initialize(BuildContext(), CancellationToken.None);

        result.DiscoveredOrgTypes.Should().NotBeNull();
        result.DiscoveredOrgTypes!.Should().HaveCount(3);
        result.DiscoveredOrgTypes!.Should().ContainSingle(o => o.TypeId == "SUPERVISORY" && o.Count == 2);
        result.DiscoveredOrgTypes!.Should().ContainSingle(o => o.TypeId == "COST_CENTER" && o.Count == 1);
        result.DiscoveredOrgTypes!.Should().ContainSingle(o => o.TypeId == "BUSINESS_UNIT" && o.Count == 1);
        // DisplayName comes from the Descriptor attribute on Organization_Type_Reference.
        result.DiscoveredOrgTypes!.Single(o => o.TypeId == "SUPERVISORY").DisplayName.Should().Be("Supervisory Organization");
    }

    [Fact]
    public async Task Initialize_orgTypeCatalog_walksAllPagesAndMergesTypes()
    {
        // A big tenant can fill page 1 with hundreds of supervisory orgs, hiding less-common types
        // like COST_CENTER on page 2+. The probe must paginate until Total_Pages so the picker
        // surfaces every type the tenant has. This test pins that behavior with a two-page response.
        var (initializer, handler) = BuildInitializer();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-organizations-page1of2.xml"));
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-organizations-page2of2.xml"));

        var result = await initializer.Initialize(BuildContext(), CancellationToken.None);

        result.DiscoveredOrgTypes.Should().NotBeNull();
        result.DiscoveredOrgTypes!.Should().HaveCount(2);
        result.DiscoveredOrgTypes!.Should().ContainSingle(o => o.TypeId == "SUPERVISORY" && o.Count == 2);
        result.DiscoveredOrgTypes!.Should().ContainSingle(o => o.TypeId == "COST_CENTER" && o.Count == 1);
    }

    [Fact]
    public async Task GetOrganizationsByType_returnsOrgsForTheTypeWithWidAndName()
    {
        // The lazy-load endpoint for the exclusions picker. The catalog fixture isn't filtered
        // server-side (FakeHttpMessageHandler doesn't introspect the request), but the parser
        // still walks every Organization element and projects (WID, Name) — exercising the bit
        // we own. A real Workday tenant would have already filtered by type via Request_Criteria.
        var (initializer, handler) = BuildInitializer();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-organizations-catalog.xml"));

        var result = await initializer.GetOrganizationsByType(BuildContext(), "SUPERVISORY", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4, "the fake handler returns whatever was enqueued; the parser projects every Organization element");
        result.Value.Select(o => o.Reference).Should().Contain("org-aaaa-0001");
        result.Value.Single(o => o.Reference == "org-aaaa-0001").DisplayName.Should().Be("Engineering");
    }

    [Theory]
    [InlineData("'); DROP TABLE--")]
    [InlineData("type with spaces")]
    public async Task GetOrganizationsByType_unsafeTypeId_returnsFailure(string unsafeId)
    {
        // The character whitelist runs before any SOAP call. A hostile or malformed type ID must
        // fail fast — never sent to Workday and never interpolated into XPath.
        var (initializer, _) = BuildInitializer();

        var result = await initializer.GetOrganizationsByType(BuildContext(), unsafeId, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("invalid");
    }

    [Fact]
    public async Task Initialize_orgCatalogFailure_doesNotFailProbeButWarns()
    {
        // If the ISU isn't granted the Organizations-and-Roles domain, Get_Organizations 500s.
        // The probe should still succeed (workers are unaffected) but the catalog comes back empty
        // and a warning surfaces so admins see why their Department picker is empty.
        var (initializer, handler) = BuildInitializer();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));
        // Reuse the auth-fault envelope as a generic SOAP failure — the parser doesn't distinguish.
        handler.EnqueueFault(File.ReadAllText("Fixtures/auth-fault.xml"));

        var result = await initializer.Initialize(BuildContext(), CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.DiscoveredOrgTypes.Should().BeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("org-type catalog"));
    }
}
