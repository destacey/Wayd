using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models;

public sealed class AzureDevOpsBoardsWorkspace : IntegrationObject<Guid>
{
    private AzureDevOpsBoardsWorkspace() { }

    [JsonConstructor]
    private AzureDevOpsBoardsWorkspace(Guid externalId, string name, string? description, Guid? workProcessId, IntegrationState<Guid>? integrationState)
    {
        ExternalId = externalId;
        Name = name;
        Description = description;
        WorkProcessId = workProcessId;
        IntegrationState = integrationState;
    }

    public Guid ExternalId { get; private init; }

    /// <summary>Gets or sets the name of the connection.</summary>
    /// <value>The name of the connection.</value>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>Gets or sets the description.</summary>
    /// <value>The connection description.</value>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>Gets or sets the work process identifier.</summary>
    public Guid? WorkProcessId { get; private set; }

    public Result Update(string name, string? description, Guid? workProcessId)
    {
        Name = name;
        Description = description;
        WorkProcessId = workProcessId;

        return Result.Success();
    }

    public static AzureDevOpsBoardsWorkspace Create(Guid externalId, string name, string? description, Guid? workProcessId)
    {
        return new AzureDevOpsBoardsWorkspace(externalId, name, description, workProcessId, null);
    }
}
