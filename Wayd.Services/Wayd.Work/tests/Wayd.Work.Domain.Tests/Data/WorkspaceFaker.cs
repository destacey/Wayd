using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;
using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkspaceFaker : PrivateConstructorFaker<Workspace>
{
    public WorkspaceFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.WorkProcessId, f => f.Random.Guid());
        RuleFor(x => x.Key, f => new WorkspaceKey($"TEST{f.Random.Int(1, 999)}"));
        RuleFor(x => x.Name, f => f.Company.CompanyName());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.OwnershipInfo, f => OwnershipInfo.CreateWaydOwned());
        RuleFor(x => x.IsActive, true);
    }
}

public static class WorkspaceFakerExtensions
{
    public static WorkspaceFaker AsExternal(
        this WorkspaceFaker faker,
        Connector connector = Connector.AzureDevOps,
        string? systemId = null,
        string? externalId = null,
        string? externalViewWorkItemUrlTemplate = null)
    {
        faker.RuleFor(x => x.Key, f => new WorkspaceKey($"EXT{f.Random.Int(1, 999)}"));
        faker.RuleFor(x => x.OwnershipInfo, f =>
        {
            var actualSystemId = systemId ?? $"sys-{f.Random.AlphaNumeric(8)}";
            var actualExternalId = externalId ?? f.Random.Guid().ToString();
            return OwnershipInfo.CreateExternalOwned(connector, actualSystemId, actualExternalId);
        });

        if (externalViewWorkItemUrlTemplate != null)
        {
            faker.RuleFor(x => x.ExternalViewWorkItemUrlTemplate, externalViewWorkItemUrlTemplate);
        }

        return faker;
    }

    public static WorkspaceFaker WithId(this WorkspaceFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static WorkspaceFaker WithWorkProcessId(this WorkspaceFaker faker, Guid workProcessId)
    {
        faker.RuleFor(x => x.WorkProcessId, workProcessId);
        return faker;
    }

    public static WorkspaceFaker WithKey(this WorkspaceFaker faker, WorkspaceKey key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static WorkspaceFaker WithName(this WorkspaceFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static WorkspaceFaker WithDescription(this WorkspaceFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static WorkspaceFaker WithIsActive(this WorkspaceFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
