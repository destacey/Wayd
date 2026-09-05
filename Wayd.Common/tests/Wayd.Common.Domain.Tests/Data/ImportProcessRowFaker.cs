using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class ImportProcessRowFaker : PrivateConstructorFaker<ImportProcessRow>
{
    public ImportProcessRowFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ImportProcessId, f => f.Random.Guid());
        RuleFor(x => x.ImportId, f => f.Random.AlphaNumeric(8));
        RuleFor(x => x.RowNumber, f => f.Random.Int(1, 500));
        RuleFor(x => x.Payload, f => $$"""{"name":"{{f.Name.FullName()}}"}""");
        RuleFor(x => x.Status, ImportRowStatus.Pending);
        RuleFor(x => x.CreatedEntityId, (Guid?)null);
        RuleFor(x => x.Error, (string?)null);
        RuleFor(x => x.AttemptedOn, (Instant?)null);
    }
}

public static class ImportProcessRowFakerExtensions
{
    public static ImportProcessRowFaker WithId(this ImportProcessRowFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ImportProcessRowFaker WithImportId(this ImportProcessRowFaker faker, string importId)
    {
        faker.RuleFor(x => x.ImportId, importId);
        return faker;
    }

    public static ImportProcessRowFaker WithRowNumber(this ImportProcessRowFaker faker, int rowNumber)
    {
        faker.RuleFor(x => x.RowNumber, rowNumber);
        return faker;
    }

    public static ImportProcessRowFaker WithPayload(this ImportProcessRowFaker faker, string? payload)
    {
        faker.RuleFor(x => x.Payload, payload);
        return faker;
    }

    public static ImportProcessRowFaker WithStatus(this ImportProcessRowFaker faker, ImportRowStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static ImportProcessRowFaker WithError(this ImportProcessRowFaker faker, string? error)
    {
        faker.RuleFor(x => x.Error, error);
        return faker;
    }

    public static ImportProcessRowFaker WithCreatedEntityId(this ImportProcessRowFaker faker, Guid? createdEntityId)
    {
        faker.RuleFor(x => x.CreatedEntityId, createdEntityId);
        return faker;
    }

    /// <summary>A row the retention sweep has emptied — failed, but no longer retryable.</summary>
    public static ImportProcessRowFaker AsPurged(this ImportProcessRowFaker faker) =>
        faker.WithStatus(ImportRowStatus.Failed).WithPayload(null);
}
