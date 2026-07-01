using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Workday.Soap;
using Wayd.Integrations.Workday.Tests.Infrastructure;
using Xunit;

namespace Wayd.Integrations.Workday.Tests;

public class WorkdayStaffingServiceTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 7, 1, 15, 30, 45, TimeSpan.Zero);

    private static WorkdayRequestContext BuildContext(
        WorkdayWorkerKey key = WorkdayWorkerKey.Wid,
        bool useUserIdAsEmailFallback = false,
        bool usePreferredName = false,
        bool normalizeNameCasing = false,
        string? departmentOrganizationTypeId = null,
        IReadOnlyList<WorkdayOrgExclusion>? orgExclusions = null,
        NodaTime.Instant? incrementalUpdatedFrom = null) => new(
        SoapEndpoint: "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1",
        TenantAlias: "acme_corp1",
        WsdlVersion: "v46.1",
        Credentials: new WorkdayCredentials("wayd_isu@acme_corp1", "secret"),
        WorkerKey: key,
        IncludeInactive: false,
        IncrementalUpdatedFrom: incrementalUpdatedFrom,
        UseUserIdAsEmailFallback: useUserIdAsEmailFallback,
        UsePreferredName: usePreferredName,
        NormalizeNameCasing: normalizeNameCasing,
        DepartmentOrganizationTypeId: departmentOrganizationTypeId,
        OrgExclusions: orgExclusions);

    private static (WorkdayStaffingService service, FakeHttpMessageHandler handler) BuildService()
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var client = new WorkdayStaffingClient(httpClient, new FixedTimeProvider(TestNow), NullLogger<WorkdayStaffingClient>.Instance);
        var service = new WorkdayStaffingService(client, NullLogger<WorkdayStaffingService>.Instance);
        return (service, handler);
    }

    [Fact]
    public async Task GetEmployees_healthy_returnsProjectedWorkers()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();
        employees.Should().HaveCount(2);

        var alex = employees.Single(e => e.Name.FirstName == "Alex");
        alex.EmployeeNumber.Should().Be("aaaa1111-bbbb-cccc-dddd-eeeeffff0001");
        alex.Email.Value.Should().Be("alex.rivera@acme.example");
        alex.JobTitle.Should().Be("Senior Engineer");
        alex.IsActive.Should().BeTrue();
        // EmployeeType prefers the Worker_Type_Reference Descriptor attribute (display value).
        alex.EmployeeType.Should().Be("Regular Employee");
        // Manager comes from the *last* entry of Management_Chain_Data (chain is CEO → direct).
        alex.ManagerEmployeeNumber.Should().Be("aaaa1111-bbbb-cccc-dddd-eeeeffff0002");

        var casey = employees.Single(e => e.Name.FirstName == "Casey");
        casey.EmployeeType.Should().Be("Contingent Worker");
    }

    [Fact]
    public async Task GetEmployees_workerKeyEmployeeId_usesBusinessIdAsEmployeeNumber()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(BuildContext(WorkdayWorkerKey.EmployeeId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();
        var alex = employees.Single(e => e.Name.FirstName == "Alex");
        alex.EmployeeNumber.Should().Be("WX-10001"); // Employee_ID becomes the key
        // Manager_Reference is resolved against the same WorkerKey, so its Employee_ID is used.
        alex.ManagerEmployeeNumber.Should().Be("WX-10002");
    }

    [Fact]
    public async Task GetEmployees_missingEmail_skipsWorker()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-missing-email.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Should().BeEmpty("workers with no usable email are skipped rather than failing the run");
    }

    [Fact]
    public async Task GetEmployees_authFailure_returnsFailureResult()
    {
        var (service, handler) = BuildService();
        handler.EnqueueFault(File.ReadAllText("Fixtures/auth-fault.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Authentication failed");
    }

    [Fact]
    public async Task GetEmployees_userIdEmailFallback_on_projectsWorkersUsingUserIdAsEmail()
    {
        var (service, handler) = BuildService();
        // Contact_Data is omitted; User_ID is email-shaped on both workers.
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-userid-email.xml"));

        var result = await service.GetEmployees(
            BuildContext(useUserIdAsEmailFallback: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();
        employees.Should().HaveCount(2);
        employees.Single(e => e.Name.FirstName == "Alex").Email.Value
            .Should().Be("alex.rivera@acme.example");
        employees.Single(e => e.Name.FirstName == "Casey").Email.Value
            .Should().Be("casey.park@acme.example");
    }

    [Fact]
    public async Task GetEmployees_userIdEmailFallback_on_skipsWorkersWhenUserIdIsNotAnEmail()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-userid-not-email.xml"));

        var result = await service.GetEmployees(
            BuildContext(useUserIdAsEmailFallback: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Should().BeEmpty(
            "fallback only accepts User_ID when it parses as a valid email — username-style values still skip the worker");
    }

    [Fact]
    public async Task GetEmployees_usePreferredName_off_readsLegalName()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-preferred-name.xml"));

        var result = await service.GetEmployees(BuildContext(usePreferredName: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();

        // Default behaviour is Legal_Name_Data → Bobby's row uses "Robert" (legal first name).
        var bobby = employees.Single(e => e.EmployeeNumber == "aaaa1111-bbbb-cccc-dddd-eeeeffff0001");
        bobby.Name.FirstName.Should().Be("Robert");
        bobby.Name.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task GetEmployees_usePreferredName_on_readsPreferredName()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-preferred-name.xml"));

        var result = await service.GetEmployees(BuildContext(usePreferredName: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();

        // Bobby has a fully-populated preferred block — should map across all three name parts.
        var bobby = employees.Single(e => e.EmployeeNumber == "aaaa1111-bbbb-cccc-dddd-eeeeffff0001");
        bobby.Name.FirstName.Should().Be("Bobby");
        bobby.Name.MiddleName.Should().Be("James");
        bobby.Name.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task GetEmployees_usePreferredName_on_fallsBackToLegalPerComponent()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-preferred-name.xml"));

        var result = await service.GetEmployees(BuildContext(usePreferredName: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();

        // Casey has a preferred first name but no preferred middle/last — those fall back to legal.
        var casey = employees.Single(e => e.EmployeeNumber == "aaaa1111-bbbb-cccc-dddd-eeeeffff0002");
        casey.Name.FirstName.Should().Be("Casey");
        casey.Name.MiddleName.Should().Be("Lee");
        casey.Name.LastName.Should().Be("Park");
    }

    [Fact]
    public async Task GetEmployees_normalizeNameCasing_off_preservesAllCaps()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-all-caps-names.xml"));

        var result = await service.GetEmployees(
            BuildContext(normalizeNameCasing: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();

        var dan = employees.Single(e => e.EmployeeNumber == "aaaa1111-bbbb-cccc-dddd-eeeeffff0001");
        dan.Name.FirstName.Should().Be("DANIEL");
        dan.Name.MiddleName.Should().Be("JONES");
        dan.Name.LastName.Should().Be("MCDONALD");

        // The mixed-case worker is unaffected either way — the heuristic only touches all-caps.
        var jose = employees.Single(e => e.EmployeeNumber == "aaaa1111-bbbb-cccc-dddd-eeeeffff0003");
        jose.Name.FirstName.Should().Be("José");
        jose.Name.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task GetEmployees_normalizeNameCasing_on_titleCasesAllCapsNames()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-all-caps-names.xml"));

        var result = await service.GetEmployees(
            BuildContext(normalizeNameCasing: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();

        // MCDONALD → McDonald (Mc inner-cap rule)
        var dan = employees.Single(e => e.EmployeeNumber == "aaaa1111-bbbb-cccc-dddd-eeeeffff0001");
        dan.Name.FirstName.Should().Be("Daniel");
        dan.Name.MiddleName.Should().Be("Jones");
        dan.Name.LastName.Should().Be("McDonald");

        // MARY-ANNE / O'BRIEN → Mary-Anne / O'Brien (hyphen + apostrophe word boundaries)
        var mary = employees.Single(e => e.EmployeeNumber == "aaaa1111-bbbb-cccc-dddd-eeeeffff0002");
        mary.Name.FirstName.Should().Be("Mary-Anne");
        mary.Name.LastName.Should().Be("O'Brien");
    }

    [Fact]
    public async Task GetEmployees_normalizeNameCasing_on_preservesMixedCaseInput()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-all-caps-names.xml"));

        var result = await service.GetEmployees(
            BuildContext(normalizeNameCasing: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.Employees.ToList();

        // José Smith is already mixed-case — the heuristic must leave it exactly as-is, including
        // the diacritic. Regression here would mean we're stomping user-curated casing.
        var jose = employees.Single(e => e.EmployeeNumber == "aaaa1111-bbbb-cccc-dddd-eeeeffff0003");
        jose.Name.FirstName.Should().Be("José");
        jose.Name.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task GetEmployees_departmentOrgTypeId_null_returnsNullDepartment()
    {
        // When the admin opted out of Department sync, no XPath is built and the field is null.
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(
            BuildContext(departmentOrganizationTypeId: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Single(e => e.Name.FirstName == "Alex").Department.Should().BeNull();
    }

    [Fact]
    public async Task GetEmployees_departmentOrgTypeId_SUPERVISORY_readsSupervisoryOrgName()
    {
        // Default Workday-shipped type. The fixture has Alex in a Supervisory org named "Engineering".
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(
            BuildContext(departmentOrganizationTypeId: "SUPERVISORY"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Single(e => e.Name.FirstName == "Alex").Department.Should().Be("Engineering");
    }

    [Fact]
    public async Task GetEmployees_departmentOrgTypeId_COST_CENTER_readsCostCenterOrgName()
    {
        // Admin override: the same worker, looked at through the COST_CENTER lens, produces a
        // different Department value. This is the whole point of making the type ID configurable.
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(
            BuildContext(departmentOrganizationTypeId: "COST_CENTER"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Single(e => e.Name.FirstName == "Alex").Department.Should().Be("R&D");
    }

    [Theory]
    [InlineData("'); DROP TABLE--")]
    [InlineData("'] | //*")]
    [InlineData("type with spaces")]
    public async Task GetEmployees_departmentOrgTypeId_unsafeValue_returnsNullDepartment(string unsafeId)
    {
        // The IsSafeOrgTypeId whitelist rejects anything outside letters/digits/underscore/hyphen.
        // A malformed or hostile value must not be interpolated into XPath; we return null instead.
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(
            BuildContext(departmentOrganizationTypeId: unsafeId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Single(e => e.Name.FirstName == "Alex").Department.Should().BeNull();
    }

    [Fact]
    public async Task GetEmployees_request_carriesWsSecurityHeader()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        await service.GetEmployees(BuildContext(), CancellationToken.None);

        var sent = handler.Captured.Single();
        var body = await sent.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("UsernameToken");
        body.Should().Contain("wayd_isu@acme_corp1");
        body.Should().Contain("secret");
        body.Should().Contain("Get_Workers_Request");
        sent.RequestUri!.ToString().Should().Be("https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1");
    }

    [Fact]
    public async Task GetEmployees_incrementalRequest_usesInjectedClockForUpdatedThrough()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        await service.GetEmployees(
            BuildContext(incrementalUpdatedFrom: NodaTime.Instant.FromUtc(2026, 6, 30, 12, 0)),
            CancellationToken.None);

        var body = await handler.Captured.Single().Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("<wd:Updated_From>2026-06-30T12:00:00Z</wd:Updated_From>");
        body.Should().Contain("<wd:Updated_Through>2026-07-01T15:30:45Z</wd:Updated_Through>");
    }

    [Fact]
    public async Task GetEmployees_orgExclusions_dropsMatchingWorkersAndReportsCount()
    {
        // The fixture has Alex in two orgs: a SUPERVISORY org (WID "org-aaaa-0001") and a
        // COST_CENTER org (WID "org-aaaa-0003"). Excluding either WID should drop Alex from the
        // projected list and increment the count for that rule. Casey isn't in any org_data block,
        // so neither rule should ever touch her.
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(
            BuildContext(orgExclusions:
            [
                new WorkdayOrgExclusion("COST_CENTER", "org-aaaa-0003", "R&D"),
            ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Should().HaveCount(1, "Alex is excluded via the COST_CENTER rule; Casey has no org-data and so passes through");
        result.Value.Employees.Single().Name.FirstName.Should().Be("Casey");

        result.Value.ExclusionCounts.Should().HaveCount(1);
        var count = result.Value.ExclusionCounts.Single();
        count.OrganizationTypeId.Should().Be("COST_CENTER");
        count.OrganizationReference.Should().Be("org-aaaa-0003");
        count.DisplayName.Should().Be("R&D");
        count.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetEmployees_orgExclusions_rulesThatDontFireAreOmittedFromCounts()
    {
        // A rule that doesn't match any worker should NOT show up in ExclusionCounts — the sync log
        // only narrates what actually happened. Admin can still verify their rules are present by
        // looking at the connection config; we don't need to clutter run history with dead rules.
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(
            BuildContext(orgExclusions:
            [
                new WorkdayOrgExclusion("SUPERVISORY", "org-nonexistent-9999", "Phantom Org"),
            ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Should().HaveCount(2, "both workers stay because the rule's target org isn't on either");
        result.Value.ExclusionCounts.Should().BeEmpty("rules that didn't match anything aren't logged");
    }

    [Fact]
    public async Task GetEmployees_orgExclusions_empty_returnsEveryone()
    {
        // Smoke test: an empty exclusion list must behave identically to the no-exclusions case
        // (no projection cost, every worker included). Pins the default-off contract.
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(
            BuildContext(orgExclusions: []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Employees.Should().HaveCount(2);
        result.Value.ExclusionCounts.Should().BeEmpty();
    }
}
