using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models;

public sealed class AzureDevOpsBoardsWorkProcess : IntegrationObject<Guid>
{
    private AzureDevOpsBoardsWorkProcess() { }

    [JsonConstructor]
    private AzureDevOpsBoardsWorkProcess(Guid externalId, string name, string? description, IntegrationState<Guid>? integrationState)
    {
        ExternalId = externalId;
        Name = name;
        Description = description;
        IntegrationState = integrationState;
    }

    public Guid ExternalId { get; private init; }

    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    public Result Update(string name, string? description)
    {
        Name = name;
        Description = description;

        return Result.Success();
    }

    public static AzureDevOpsBoardsWorkProcess Create(Guid externalId, string name, string? description)
    {
        return new AzureDevOpsBoardsWorkProcess(externalId, name, description, null);
    }
}
