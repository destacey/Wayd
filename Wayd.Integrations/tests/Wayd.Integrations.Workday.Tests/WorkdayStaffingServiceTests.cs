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
    private static WorkdayConnectionCredentials BuildCredentials(
        WorkdayWorkerKey key = WorkdayWorkerKey.Wid,
        bool useUserIdAsEmailFallback = false) => new(
        SoapEndpoint: "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1",
        TenantAlias: "acme_corp1",
        WsdlVersion: "v46.1",
        IsuUsername: "wayd_isu@acme_corp1",
        IsuPassword: "secret",
        WorkerKey: key,
        IncludeInactive: false,
        IncrementalUpdatedFrom: null,
        UseUserIdAsEmailFallback: useUserIdAsEmailFallback);

    private static (WorkdayStaffingService service, FakeHttpMessageHandler handler) BuildService()
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var client = new WorkdayStaffingClient(httpClient, NullLogger<WorkdayStaffingClient>.Instance);
        var service = new WorkdayStaffingService(client, NullLogger<WorkdayStaffingService>.Instance);
        return (service, handler);
    }

    [Fact]
    public async Task GetEmployees_healthy_returnsProjectedWorkers()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(BuildCredentials(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.ToList();
        employees.Should().HaveCount(2);

        var dannie = employees.Single(e => e.Name.FirstName == "Dannie");
        dannie.EmployeeNumber.Should().Be("aaaa1111-bbbb-cccc-dddd-eeeeffff0001");
        dannie.Email.Value.Should().Be("dannie.stacey@acme.example");
        dannie.JobTitle.Should().Be("Senior Engineer");
        dannie.IsActive.Should().BeTrue();
        // EmployeeType prefers the Worker_Type_Reference Descriptor attribute (display value).
        dannie.EmployeeType.Should().Be("Regular Employee");
        // Manager comes from the *last* entry of Management_Chain_Data (chain is CEO → direct).
        dannie.ManagerEmployeeNumber.Should().Be("aaaa1111-bbbb-cccc-dddd-eeeeffff0002");

        var casey = employees.Single(e => e.Name.FirstName == "Casey");
        casey.EmployeeType.Should().Be("Contingent Worker");
    }

    [Fact]
    public async Task GetEmployees_workerKeyEmployeeId_usesBusinessIdAsEmployeeNumber()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        var result = await service.GetEmployees(BuildCredentials(WorkdayWorkerKey.EmployeeId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.ToList();
        var dannie = employees.Single(e => e.Name.FirstName == "Dannie");
        dannie.EmployeeNumber.Should().Be("W-1001"); // Employee_ID becomes the key
        // Manager_Reference is resolved against the same WorkerKey, so its Employee_ID is used.
        dannie.ManagerEmployeeNumber.Should().Be("W-1002");
    }

    [Fact]
    public async Task GetEmployees_missingEmail_skipsWorker()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-missing-email.xml"));

        var result = await service.GetEmployees(BuildCredentials(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("workers with no usable email are skipped rather than failing the run");
    }

    [Fact]
    public async Task GetEmployees_authFailure_returnsFailureResult()
    {
        var (service, handler) = BuildService();
        handler.EnqueueFault(File.ReadAllText("Fixtures/auth-fault.xml"));

        var result = await service.GetEmployees(BuildCredentials(), CancellationToken.None);

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
            BuildCredentials(useUserIdAsEmailFallback: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var employees = result.Value.ToList();
        employees.Should().HaveCount(2);
        employees.Single(e => e.Name.FirstName == "Dannie").Email.Value
            .Should().Be("dannie.stacey@triowfs.com");
        employees.Single(e => e.Name.FirstName == "Casey").Email.Value
            .Should().Be("casey.park@triowfs.com");
    }

    [Fact]
    public async Task GetEmployees_userIdEmailFallback_on_skipsWorkersWhenUserIdIsNotAnEmail()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-userid-not-email.xml"));

        var result = await service.GetEmployees(
            BuildCredentials(useUserIdAsEmailFallback: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty(
            "fallback only accepts User_ID when it parses as a valid email — username-style values still skip the worker");
    }

    [Fact]
    public async Task GetEmployees_request_carriesWsSecurityHeader()
    {
        var (service, handler) = BuildService();
        handler.EnqueueXml(File.ReadAllText("Fixtures/get-workers-healthy-page1.xml"));

        await service.GetEmployees(BuildCredentials(), CancellationToken.None);

        var sent = handler.Captured.Single();
        var body = await sent.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Contain("UsernameToken");
        body.Should().Contain("wayd_isu@acme_corp1");
        body.Should().Contain("secret");
        body.Should().Contain("Get_Workers_Request");
        sent.RequestUri!.ToString().Should().Be("https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1");
    }
}
