using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class ImportProcessFaker : PrivateConstructorFaker<ImportProcess>
{
    public ImportProcessFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ImportType, "employees");
        RuleFor(x => x.Status, ImportProcessStatus.Queued);
        RuleFor(x => x.SubmissionGroupId, (Guid?)null);
        RuleFor(x => x.LastAttemptCorrelationId, (string?)null);
        RuleFor(x => x.SubmittedByUserId, f => f.Random.Guid());
        RuleFor(x => x.SubmittedOn, Instant.FromUtc(2026, 9, 5, 9, 0, 0));
        RuleFor(x => x.StartedOn, (Instant?)null);
        RuleFor(x => x.CompletedOn, (Instant?)null);
        RuleFor(x => x.LastProgressOn, (Instant?)null);
        RuleFor(x => x.TotalRowCount, 0);
        RuleFor(x => x.SucceededRowCount, 0);
        RuleFor(x => x.FailedRowCount, 0);
        RuleFor(x => x.Error, (string?)null);
    }
}

public static class ImportProcessFakerExtensions
{
    /// <summary>Stand-in for the trace id the runner supplies.</summary>
    public const string AttemptCorrelationId = "trace-0001";

    public static ImportProcessFaker WithId(this ImportProcessFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ImportProcessFaker WithImportType(this ImportProcessFaker faker, string importType)
    {
        faker.RuleFor(x => x.ImportType, importType);
        return faker;
    }

    public static ImportProcessFaker WithStatus(this ImportProcessFaker faker, ImportProcessStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static ImportProcessFaker WithSubmissionGroupId(this ImportProcessFaker faker, Guid? submissionGroupId)
    {
        faker.RuleFor(x => x.SubmissionGroupId, submissionGroupId);
        return faker;
    }

    public static ImportProcessFaker WithSubmittedByUserId(this ImportProcessFaker faker, Guid userId)
    {
        faker.RuleFor(x => x.SubmittedByUserId, userId);
        return faker;
    }

    public static ImportProcessFaker WithLastProgressOn(this ImportProcessFaker faker, Instant? lastProgressOn)
    {
        faker.RuleFor(x => x.LastProgressOn, lastProgressOn);
        return faker;
    }

    public static ImportProcessFaker WithTotalRowCount(this ImportProcessFaker faker, int totalRowCount)
    {
        faker.RuleFor(x => x.TotalRowCount, totalRowCount);
        return faker;
    }

    public static ImportProcessFaker WithSucceededRowCount(this ImportProcessFaker faker, int succeededRowCount)
    {
        faker.RuleFor(x => x.SucceededRowCount, succeededRowCount);
        return faker;
    }

    public static ImportProcessFaker WithFailedRowCount(this ImportProcessFaker faker, int failedRowCount)
    {
        faker.RuleFor(x => x.FailedRowCount, failedRowCount);
        return faker;
    }

    public static ImportProcessFaker WithError(this ImportProcessFaker faker, string? error)
    {
        faker.RuleFor(x => x.Error, error);
        return faker;
    }

    /// <summary>
    /// Builds a queued run through the real factory, so the rows and <c>TotalRowCount</c> agree the way the
    /// aggregate maintains them rather than the way a rule set happens to be configured.
    /// </summary>
    public static ImportProcess AsQueuedWith(
        this ImportProcessFaker faker,
        int rowCount,
        string importType = "employees",
        Guid? submissionGroupId = null)
    {
        var shell = faker.Generate();
        var rows = Enumerable.Range(1, rowCount)
            .Select(i => ImportProcessRow.Create($"r-{i}", i, $$"""{"row":{{i}}}"""));

        return ImportProcess.Create(importType, shell.SubmittedByUserId, submissionGroupId, rows, shell.SubmittedOn);
    }

    /// <summary>A run a worker has already claimed — the starting point for progress and cancellation tests.</summary>
    public static ImportProcess AsProcessingWith(this ImportProcessFaker faker, int rowCount, Instant startedOn)
    {
        var process = faker.AsQueuedWith(rowCount);
        process.Start(AttemptCorrelationId, startedOn);
        return process;
    }
}
