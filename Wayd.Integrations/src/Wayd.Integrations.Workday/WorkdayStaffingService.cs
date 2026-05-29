using System.Globalization;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Models;
using Wayd.Integrations.Workday.Model;
using Wayd.Integrations.Workday.Soap;

namespace Wayd.Integrations.Workday;

/// <summary>
/// Bulk-sync implementation of <see cref="IWorkdayEmployeeSource"/>. Pages through Get_Workers,
/// projects each worker into <see cref="WorkdayEmployee"/> via <see cref="WorkerFieldReader"/>,
/// and returns the flat list to the runner.
/// </summary>
public sealed class WorkdayStaffingService : IWorkdayEmployeeSource
{
    // 100 is the Workday-recommended max per page for Get_Workers; bumping further trades latency
    // for throughput and tends to bump up against per-call timeouts on large tenants.
    private const int SyncPageSize = 100;

    private readonly WorkdayStaffingClient _client;
    private readonly ILogger<WorkdayStaffingService> _logger;

    public WorkdayStaffingService(WorkdayStaffingClient client, ILogger<WorkdayStaffingService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<IExternalEmployee>>> GetEmployees(WorkdayConnectionCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            var employees = new List<WorkdayEmployee>();
            var page = 1;
            int totalPages;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await _client.GetWorkers(credentials, page, SyncPageSize, cancellationToken);
                totalPages = response.TotalPages;

                foreach (var worker in response.Workers)
                {
                    var projected = TryProject(worker, credentials.WorkerKey);
                    if (projected is not null)
                        employees.Add(projected);
                }

                _logger.LogInformation(
                    "Workday Get_Workers page {Page}/{TotalPages} returned {Count} workers ({ProjectedCount} after projection).",
                    page, totalPages, response.Workers.Count, employees.Count);

                page++;
            } while (page <= totalPages);

            EmitNullRateWarnings(employees);

            return Result.Success<IEnumerable<IExternalEmployee>>(employees);
        }
        catch (WorkdaySoapException ex)
        {
            _logger.LogError(ex, "Workday Get_Workers failed for endpoint {Endpoint}.", credentials.SoapEndpoint);
            return Result.Failure<IEnumerable<IExternalEmployee>>($"Workday Get_Workers failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Workday Get_Workers could not reach endpoint {Endpoint}.", credentials.SoapEndpoint);
            return Result.Failure<IEnumerable<IExternalEmployee>>($"Unable to reach Workday: {ex.Message}");
        }
    }

    private WorkdayEmployee? TryProject(XElement worker, WorkdayWorkerKey workerKey)
    {
        var wid = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.WorkerWid);
        var employeeId = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.EmployeeId);

        // The upsert key choice drives EmployeeNumber. If the admin chose EmployeeId but a worker
        // doesn't have one, we fall back to the WID so the row still syncs — better to capture
        // the person than to silently drop them.
        var employeeNumber = workerKey switch
        {
            WorkdayWorkerKey.EmployeeId => employeeId ?? wid,
            _ => wid,
        };

        if (string.IsNullOrWhiteSpace(employeeNumber))
        {
            _logger.LogWarning("Skipping Workday worker with no usable identifier.");
            return null;
        }

        var firstName = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.FirstName);
        var lastName = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.LastName);
        var email = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.WorkEmail);

        // We Guard.Against.NullOrWhiteSpace inside PersonName / EmailAddress; rather than throwing
        // mid-sync, skip the worker and surface a count in the log.
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Skipping Workday worker {EmployeeNumber}: missing required field (firstName/lastName/email).", employeeNumber);
            return null;
        }

        var managerKey = workerKey switch
        {
            WorkdayWorkerKey.EmployeeId => WorkerFieldReader.GetValue(worker, WorkerFieldPaths.ManagerEmployeeId)
                                            ?? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.ManagerWorkerWid),
            _ => WorkerFieldReader.GetValue(worker, WorkerFieldPaths.ManagerWorkerWid),
        };

        var isActive = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.Active) is "1" or "true";

        // Worker_Type_Reference: prefer the Descriptor attribute (tenant display value), fall back
        // to the Employee_Type_ID code if the descriptor is empty.
        var employeeType =
            WorkerFieldReader.GetAttributeValue(worker, "Descriptor", WorkerFieldPaths.WorkerTypeReference)
            ?? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.WorkerTypeId);

        return new WorkdayEmployee(
            employeeNumber: employeeNumber,
            name: new PersonName(firstName, WorkerFieldReader.GetValue(worker, WorkerFieldPaths.MiddleName), lastName),
            email: new EmailAddress(email),
            hireDate: ParseWorkdayDate(WorkerFieldReader.GetValue(worker, WorkerFieldPaths.HireDate)),
            jobTitle: WorkerFieldReader.GetValue(worker, WorkerFieldPaths.JobTitle),
            department: WorkerFieldReader.GetValue(worker, WorkerFieldPaths.Department),
            officeLocation: WorkerFieldReader.GetValue(worker, WorkerFieldPaths.OfficeLocation),
            managerEmployeeNumber: managerKey,
            isActive: isActive,
            employeeType: employeeType);
    }

    private static Instant? ParseWorkdayDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Workday emits ISO 8601 dates: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ depending on the field.
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return Instant.FromDateTimeOffset(dto);
        return null;
    }

    /// <summary>
    /// Observability hook: when a typically-populated field comes back null on more than half of
    /// the workers in a single run, something on the Workday side probably changed (deprecated
    /// field path, schema drift). Log it so we notice before customers do.
    /// </summary>
    private void EmitNullRateWarnings(List<WorkdayEmployee> employees)
    {
        if (employees.Count == 0) return;

        var halfThreshold = employees.Count / 2;

        var nullJobTitle = employees.Count(e => string.IsNullOrEmpty(e.JobTitle));
        if (nullJobTitle > halfThreshold)
            _logger.LogWarning("Workday sync: {NullCount}/{Total} workers have a null JobTitle — verify the response group / field path is still valid.", nullJobTitle, employees.Count);

        var nullDepartment = employees.Count(e => string.IsNullOrEmpty(e.Department));
        if (nullDepartment > halfThreshold)
            _logger.LogWarning("Workday sync: {NullCount}/{Total} workers have a null Department — verify the response group / field path is still valid.", nullDepartment, employees.Count);

        var nullManager = employees.Count(e => string.IsNullOrEmpty(e.ManagerEmployeeNumber));
        if (nullManager > halfThreshold)
            _logger.LogWarning("Workday sync: {NullCount}/{Total} workers have no Manager reference — verify Include_Management_Chain_Data is set and the chain XPath still matches the tenant's response shape.", nullManager, employees.Count);
    }
}
