using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Workday.Soap;

namespace Wayd.Integrations.Workday;

/// <summary>
/// Init/probe implementation. Pulls a small page (~10 workers) via Get_Workers, inspects
/// element presence to detect ISSG permission gaps, and reports a structured result. Does not
/// write to the Employees table.
/// </summary>
public sealed class WorkdayConnectionInitializer : IWorkdayConnectionInitializer
{
    private const int ProbeSampleSize = 10;

    private readonly WorkdayStaffingClient _client;
    private readonly ILogger<WorkdayConnectionInitializer> _logger;

    public WorkdayConnectionInitializer(WorkdayStaffingClient client, ILogger<WorkdayConnectionInitializer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ConnectionInitResult> Initialize(WorkdayConnectionCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            // Probe is always a full-snapshot fetch — we want a representative slice, not a delta.
            var probeCredentials = credentials with { IncrementalUpdatedFrom = null };
            var response = await _client.GetWorkers(probeCredentials, page: 1, pageSize: ProbeSampleSize, cancellationToken);

            var workers = response.Workers;
            if (workers.Count == 0)
            {
                return new ConnectionInitResult(
                    IsValid: false,
                    WorkersProbed: 0,
                    MissingRequiredFields: [],
                    Warnings: ["Workday returned zero workers. Verify the ISU's group includes at least one populated supervisory organization."],
                    AuthError: null);
            }

            var requiredFields = BuildRequiredFieldChecks(credentials.WorkerKey);

            // A field is "missing" only when its element is absent from EVERY sampled worker.
            // Present-but-empty or present-on-some is data variance, not a permission gap.
            var missing = new List<string>();
            foreach (var (label, candidates) in requiredFields)
            {
                var anyHas = workers.Any(w => WorkerFieldReader.HasElement(w, candidates));
                if (!anyHas)
                    missing.Add(label);
            }

            var warnings = BuildWarnings(workers);

            var isValid = missing.Count == 0;
            return new ConnectionInitResult(
                IsValid: isValid,
                WorkersProbed: workers.Count,
                MissingRequiredFields: missing,
                Warnings: warnings,
                AuthError: null);
        }
        catch (WorkdaySoapException ex) when (ex.IsAuthFailure)
        {
            _logger.LogWarning(ex, "Workday init probe failed authentication for endpoint {Endpoint}.", credentials.SoapEndpoint);
            return new ConnectionInitResult(
                IsValid: false,
                WorkersProbed: 0,
                MissingRequiredFields: [],
                Warnings: [],
                AuthError: $"Authentication failed: {ex.Message}");
        }
        catch (WorkdaySoapException ex)
        {
            _logger.LogWarning(ex, "Workday init probe returned an error for endpoint {Endpoint}.", credentials.SoapEndpoint);
            return new ConnectionInitResult(
                IsValid: false,
                WorkersProbed: 0,
                MissingRequiredFields: [],
                Warnings: [$"Workday returned an error: {ex.Message}"],
                AuthError: null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Workday init probe could not reach endpoint {Endpoint}.", credentials.SoapEndpoint);
            return new ConnectionInitResult(
                IsValid: false,
                WorkersProbed: 0,
                MissingRequiredFields: [],
                Warnings: [$"Unable to reach Workday at {credentials.SoapEndpoint}: {ex.Message}"],
                AuthError: null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConnectionInitResult(
                IsValid: false,
                WorkersProbed: 0,
                MissingRequiredFields: [],
                Warnings: ["Workday did not respond within the timeout. Verify the endpoint URL is correct and the tenant is reachable."],
                AuthError: null);
        }
    }

    private static IReadOnlyList<(string Label, string[] Candidates)> BuildRequiredFieldChecks(WorkdayWorkerKey workerKey)
    {
        var checks = new List<(string, string[])>
        {
            ("Worker WID",   WorkerFieldPaths.WorkerWid),
            ("First Name",   WorkerFieldPaths.FirstName),
            ("Last Name",    WorkerFieldPaths.LastName),
            ("Work Email",   WorkerFieldPaths.WorkEmail),
            ("Active Flag",  WorkerFieldPaths.Active),
        };

        // Employee_ID is only load-bearing when the admin selected it as the upsert key.
        if (workerKey == WorkdayWorkerKey.EmployeeId)
            checks.Add(("Employee ID", WorkerFieldPaths.EmployeeId));

        return checks;
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<System.Xml.Linq.XElement> workers)
    {
        var warnings = new List<string>();

        // Optional fields where "no one has it across 10 workers" is suspicious enough to flag.
        var optional = new (string Label, string[] Candidates)[]
        {
            ("Job Title",      WorkerFieldPaths.JobTitle),
            ("Department",     WorkerFieldPaths.Department),
            ("Manager",        WorkerFieldPaths.ManagerWorkerWid),
        };

        foreach (var (label, candidates) in optional)
        {
            var anyHas = workers.Any(w => WorkerFieldReader.HasElement(w, candidates));
            if (!anyHas)
                warnings.Add($"None of the sampled workers had a {label}. Sync will treat this field as null for all employees.");
        }

        return warnings;
    }
}
