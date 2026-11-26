using System.Net;
using System.Text;
using System.Xml.Linq;
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
    private static readonly DateTimeOffset _testNow = new(2026, 7, 1, 15, 30, 45, TimeSpan.Zero);

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
        var client = new WorkdayStaffingClient(httpClient, new FixedTimeProvider(_testNow), NullLogger<WorkdayStaffingClient>.Instance);
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
    public async Task GetEmployees_multipleWorkEmails_collectsAllOfThem()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-multiple-emails.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var avery = result.Value.Employees.Single(e => e.Name.FirstName == "Avery");
        avery.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(
            ["avery.chen@acme.example", "avery.chen@acme-legacy.example"]);
    }

    [Fact]
    public async Task GetEmployees_homeTypedAddress_isExcluded()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-multiple-emails.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        var avery = result.Value.Employees.Single(e => e.Name.FirstName == "Avery");
        avery.Emails.Select(e => e.Email.Value).Should().NotContain("avery.personal@mailbox.example",
            "HOME addresses are personal data and are useless for cross-system matching");
    }

    [Fact]
    public async Task GetEmployees_primaryFlaggedAddress_becomesTheCanonicalEmail()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-multiple-emails.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        var avery = result.Value.Employees.Single(e => e.Name.FirstName == "Avery");
        // The Primary-flagged entry is listed second, so document order would pick the legacy one.
        avery.Email.Value.Should().Be("avery.chen@acme.example");
        avery.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be("avery.chen@acme.example");
    }

    [Fact]
    public async Task GetEmployees_nonPublicAddress_isExcluded()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-multiple-emails.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        var jordan = result.Value.Employees.Single(e => e.Name.FirstName == "Jordan");
        // "true"/"false" spelling of the attributes, and the hidden address stays hidden.
        jordan.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(["jordan.blake@acme.example"]);
    }

    [Fact]
    public async Task GetEmployees_absentPublicAttribute_treatsAddressAsPublic()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-multiple-emails.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        var sam = result.Value.Employees.Single(e => e.Name.FirstName == "Sam");
        sam.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(["sam.ortiz@acme.example"]);
        sam.Emails.Single().IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task GetEmployees_singleEmail_stillPopulatesTheCollection()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(BuildContext(), CancellationToken.None);

        var alex = result.Value.Employees.Single(e => e.Name.FirstName == "Alex");
        alex.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(["alex.rivera@acme.example"]);
    }

    [Fact]
    public async Task GetEmployees_userIdEmailFallback_includesTheFallbackAddressInTheCollection()
    {
        var (service, handler) = BuildService();
        // Contact_Data is absent entirely, so the collection can only come from the fallback.
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-userid-email.xml"));

        var result = await service.GetEmployees(BuildContext(useUserIdAsEmailFallback: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employee = result.Value.Employees[0];
        employee.Emails.Should().ContainSingle();
        employee.Emails.Single().Email.Value.Should().Be(employee.Email.Value);
        employee.Emails.Single().IsPrimary.Should().BeTrue();
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

    [Fact]
    public async Task GetEmployees_errorWithoutSoapFault_doesNotEchoTheBody()
    {
        // Arrange
        var (service, handler) = BuildService();
        handler.EnqueueXml("<html><body>internal-admin-console secret=hunter2</body></html>", HttpStatusCode.InternalServerError);

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotContain("hunter2").And.Contain("500");
    }

    [Fact]
    public async Task GetEmployees_reportedPagesAboveTheCap_stopsAfterTheFirstPage()
    {
        // Arrange
        var (service, handler) = BuildService();
        var page = File.ReadAllText("Fixtures/get-workers-healthy-page1.xml")
            .Replace("<wd:Total_Pages>1</wd:Total_Pages>", $"<wd:Total_Pages>{WorkdayStaffingService.MaxSyncPages + 1}</wd:Total_Pages>");
        handler.EnqueueXml(page);

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("page limit");
        handler.Captured.Should().ContainSingle();
    }

    [Fact]
    public async Task GetWorkers_reportsTheResponseSize()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var body = File.ReadAllText("Fixtures/get-workers-healthy-page1.xml");
        handler.EnqueueXml(body);
        var client = new WorkdayStaffingClient(new HttpClient(handler), new FixedTimeProvider(_testNow), NullLogger<WorkdayStaffingClient>.Instance);

        // Act
        var response = await client.GetWorkers(BuildContext(), 1, WorkdayStaffingService.SyncPageSize, _ => true, TestContext.Current.CancellationToken);

        // Assert
        response.ResponseBytes.Should().Be(Encoding.UTF8.GetByteCount(body));
    }

    [Fact]
    public async Task GetEmployees_pageWithMoreWorkersThanRequested_stopsTheSync()
    {
        // Arrange
        var (service, handler) = BuildService();
        var page = XDocument.Parse(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));
        var worker = page.Descendants().First(e => e.Name.LocalName == "Worker");
        for (var i = 0; i < WorkdayStaffingService.SyncPageSize; i++)
            worker.AddAfterSelf(new XElement(worker));
        handler.EnqueueXml(page.ToString());

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than the 200 requested");
    }

    [Fact]
    public async Task GetEmployees_declaredBodyAboveTheCap_failsWithoutReadingIt()
    {
        // Arrange
        var (service, handler) = BuildService();
        var content = new StringContent("<x/>");
        content.Headers.ContentLength = WorkdayStaffingClient.MaxResponseBytes + 1L;
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MB limit").And.Contain("It declared 1,024 MB");
    }

    [Fact]
    public async Task GetEmployees_undeclaredBodyAboveTheCap_stopsReadingAtTheCap()
    {
        // Arrange
        var (service, handler) = BuildService();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new EndlessStream()) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MB limit").And.Contain(WorkdayStaffingClient.TooLargeMessage);
    }

    [Fact]
    public async Task GetEmployees_bodyCutOffPartWay_reportsItWasCutOff()
    {
        // Arrange
        var (service, handler) = BuildService();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new FailingStream()) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cut off").And.NotContain("MB limit");
    }

    [Fact]
    public async Task GetEmployees_httpEndpoint_sendsNothing()
    {
        // Arrange
        var (service, handler) = BuildService();
        var context = BuildContext() with
        {
            SoapEndpoint = "http://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1",
        };

        // Act
        var result = await service.GetEmployees(context, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(WorkdayStaffingClient.NotHttpsMessage);
        handler.Captured.Should().BeEmpty("the envelope carries the ISU password");
    }

    [Fact]
    public async Task GetEmployees_bodyTheRetryCheckGaveUpOn_reportsTheRecordedReason()
    {
        // Arrange
        var fake = new FakeHttpMessageHandler();
        fake.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new ConsumedContent() });
        var client = new WorkdayStaffingClient(
            new HttpClient(new GivingUpRetryCheck("Workday's error response did not arrive in full within 30 seconds.") { InnerHandler = fake }),
            new FixedTimeProvider(_testNow),
            NullLogger<WorkdayStaffingClient>.Instance);
        var service = new WorkdayStaffingService(client, NullLogger<WorkdayStaffingService>.Instance);

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("within 30 seconds");
    }

    [Fact]
    public async Task GetEmployees_pageOfSmallWorkersLargerThan256Mb_syncsEveryWorker()
    {
        // Arrange
        var (service, handler) = BuildService();
        var page = new GeneratedPage(WorkdayStaffingService.SyncPageSize, workerPaddingBytes: 1_400_000);
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(page) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Employees.Should().HaveCount(WorkdayStaffingService.SyncPageSize);
        page.BytesSent.Should().BeGreaterThan(256L * 1024 * 1024);
    }

    [Fact]
    public async Task GetEmployees_workerAboveTheRecordCap_stopsReadingAtTheCap()
    {
        // Arrange
        var (service, handler) = BuildService();
        var page = new GeneratedPage(workers: 1, workerPaddingBytes: WorkdayStaffingClient.MaxRecordBytes * 2);
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(page) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(WorkdayStaffingClient.RecordTooLargeMessage);
        page.BytesSent.Should().BeLessThan(WorkdayStaffingClient.MaxRecordBytes + 1024L * 1024);
    }

    [Fact]
    public async Task GetEmployees_whitespaceThatNeverEnds_stopsReadingAtTheNodeCap()
    {
        // Arrange
        var (service, handler) = BuildService();
        var body = new EndlessStream(nodeBytes: int.MaxValue);
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(WorkdayStaffingClient.RecordTooLargeMessage);
        body.BytesSent.Should().BeLessThan(WorkdayStaffingClient.MaxRecordBytes + 1024L * 1024);
    }

    [Fact]
    public async Task GetEmployees_failedResponseThatNeverEnds_readsOnlyItsStart()
    {
        // Arrange
        var (service, handler) = BuildService();
        var body = new EndlessStream();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StreamContent(body) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("HTTP 500 without a SOAP fault");
        body.BytesSent.Should().Be(WorkdayStaffingClient.MaxFaultResponseBytes);
    }

    [Fact]
    public async Task GetEmployees_pageWithMoreWorkersThanRequested_stopsReadingIt()
    {
        // Arrange
        var (service, handler) = BuildService();
        var page = new GeneratedPage(workers: 10_000, workerPaddingBytes: 0);
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(page) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than the 200 requested");
        page.Finished.Should().BeFalse("reading stops at the first worker past the page size");
    }

    [Fact]
    public async Task GetEmployees_elementsNestedWithoutEnd_stopsReadingAtTheDepthCap()
    {
        // Arrange
        var (service, handler) = BuildService();
        var body = new PatternStream("", _ => "<a>");
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(WorkdayStaffingClient.TooDeepMessage);
        body.BytesSent.Should().BeLessThan(1024 * 1024);
    }

    [Fact]
    public async Task GetEmployees_namesThatNeverRepeat_stopsReadingAtTheNameCap()
    {
        // Arrange
        var (service, handler) = BuildService();
        var body = new PatternStream("<r>", i => $"<n{i}/>");
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(WorkdayStaffingClient.TooManyNamesMessage);
        body.BytesSent.Should().BeLessThan(1024 * 1024);
    }

    [Fact]
    public async Task GetEmployees_faultWithAByteOrderMark_reportsTheFault()
    {
        // Arrange
        var (service, handler) = BuildService();
        var body = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(File.ReadAllText("Fixtures/auth-fault.xml"))).ToArray();
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = content });

        // Act
        var result = await service.GetEmployees(BuildContext(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Authentication failed: invalid user or password");
    }

    [Fact]
    public async Task GetOrganizations_pageWithMoreOrganizationsThanRequested_fails()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-organizations-catalog.xml"));
        var client = new WorkdayStaffingClient(new HttpClient(handler), new FixedTimeProvider(_testNow), NullLogger<WorkdayStaffingClient>.Instance);

        // Act
        var act = () => client.GetOrganizations(BuildContext(), 1, 3, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<WorkdaySoapException>().WithMessage("*more than the 3 requested organizations*");
    }

    /// <summary>
    /// A Get_Workers page generated as it is read, so a page far larger than any buffer the test could hold is
    /// sent without being held. Each worker is the healthy fixture's first, padded with an element the projection
    /// never reads.
    /// </summary>
    private sealed class GeneratedPage : Stream
    {
        private readonly IEnumerator<ReadOnlyMemory<byte>> _segments;
        private ReadOnlyMemory<byte> _current;

        public GeneratedPage(int workers, int workerPaddingBytes)
        {
            var fixture = XDocument.Parse(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));
            var worker = fixture.Descendants(WorkdayStaffingClient.Wd + "Worker").First();
            var data = worker.Parent!;
            worker.Remove();
            data.RemoveNodes();
            data.Add(new XText("|"));
            var (head, tail) = Split(fixture.ToString(SaveOptions.DisableFormatting), "|");

            worker.Add(new XElement(WorkdayStaffingClient.Wd + "Padding", "|"));
            var (workerHead, workerTail) = Split(worker.ToString(SaveOptions.DisableFormatting), "|");

            _segments = Segments(head, tail, workerHead, workerTail, workers, workerPaddingBytes).GetEnumerator();
        }

        /// <summary>The bytes of the page read so far.</summary>
        public long BytesSent { get; private set; }

        /// <summary>Whether the page has been read to its end.</summary>
        public bool Finished { get; private set; }

        private static (byte[] Head, byte[] Tail) Split(string xml, string marker)
        {
            var at = xml.IndexOf(marker, StringComparison.Ordinal);
            return (Encoding.UTF8.GetBytes(xml[..at]), Encoding.UTF8.GetBytes(xml[(at + marker.Length)..]));
        }

        private static IEnumerable<ReadOnlyMemory<byte>> Segments(byte[] head, byte[] tail, byte[] workerHead, byte[] workerTail, int workers, int paddingBytes)
        {
            var padding = new byte[64 * 1024];
            Array.Fill(padding, (byte)'x');

            yield return head;
            for (var i = 0; i < workers; i++)
            {
                yield return workerHead;
                for (var left = paddingBytes; left > 0; left -= padding.Length)
                    yield return padding.AsMemory(0, Math.Min(left, padding.Length));
                yield return workerTail;
            }
            yield return tail;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (_current.IsEmpty)
            {
                if (!_segments.MoveNext())
                {
                    Finished = true;
                    return 0;
                }
                _current = _segments.Current;
            }

            var read = Math.Min(count, _current.Length);
            _current[..read].CopyTo(buffer.AsMemory(offset, read));
            _current = _current[read..];
            BytesSent += read;
            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// A body with no declared length that never ends: <c>prefix</c>, then the text <c>unit</c> gives for each
    /// index in turn.
    /// </summary>
    private sealed class PatternStream(string prefix, Func<long, string> unit) : Stream
    {
        private ReadOnlyMemory<byte> _current = Encoding.UTF8.GetBytes(prefix);
        private long _index;

        /// <summary>The bytes read so far.</summary>
        public long BytesSent { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (_current.IsEmpty)
                _current = Encoding.UTF8.GetBytes(unit(_index++));

            var read = Math.Min(count, _current.Length);
            _current[..read].CopyTo(buffer.AsMemory(offset, read));
            _current = _current[read..];
            BytesSent += read;
            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Records why it gave up on the body, as the resilience pipeline's retry check does.</summary>
    private sealed class GivingUpRetryCheck(string reason) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            response.RequestMessage = request;
            request.Options.Set(WorkdayStaffingClient.UnreadBodyReason, reason);
            return response;
        }
    }

    /// <summary>
    /// A network response body that something has already started reading: like the handler's own content, it
    /// refuses to be read again.
    /// </summary>
    private sealed class ConsumedContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            throw new InvalidOperationException("The stream was already consumed. It cannot be read again.");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>A body whose connection drops after the first read.</summary>
    private sealed class FailingStream : Stream
    {
        private bool _readOnce;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_readOnce)
                throw new IOException("The connection was reset.");
            _readOnce = true;
            buffer[offset] = (byte)'<';
            return 1;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// A body with no declared length that never ends, and is well-formed XML for as long as it is read: an open
    /// element holding runs of whitespace, <paramref name="nodeBytes"/> long, each closed by an empty element.
    /// </summary>
    private sealed class EndlessStream(int nodeBytes = 1024 * 1024) : Stream
    {
        private static readonly byte[] _open = "<r>"u8.ToArray();
        private static readonly byte[] _node = "<a/>"u8.ToArray();

        /// <summary>The bytes read so far.</summary>
        public long BytesSent { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read;
            if (BytesSent < _open.Length)
            {
                read = Math.Min(count, _open.Length - (int)BytesSent);
                _open.AsSpan((int)BytesSent, read).CopyTo(buffer.AsSpan(offset));
            }
            else
            {
                var inPeriod = (BytesSent - _open.Length) % ((long)nodeBytes + _node.Length);
                if (inPeriod < nodeBytes)
                {
                    read = (int)Math.Min(count, nodeBytes - inPeriod);
                    buffer.AsSpan(offset, read).Fill((byte)' ');
                }
                else
                {
                    read = Math.Min(count, _node.Length - (int)(inPeriod - nodeBytes));
                    _node.AsSpan((int)(inPeriod - nodeBytes), read).CopyTo(buffer.AsSpan(offset));
                }
            }

            BytesSent += read;
            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
