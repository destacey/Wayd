using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models.Entra;

public sealed class EntraConnection : Connection<EntraConnectionConfiguration>, ISyncableConnection
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private EntraConnection() { }
#pragma warning restore CS8618

    private EntraConnection(
        string name,
        string? description,
        bool configurationIsValid,
        EntraConnectionConfiguration configuration)
    {
        Name = name;
        Description = description;
        IsValidConfiguration = configurationIsValid;
        Connector = Wayd.Common.Domain.Enums.AppIntegrations.Connector.Entra;
        Configuration = Guard.Against.Null(configuration, nameof(Configuration));
    }

    public override EntraConnectionConfiguration Configuration { get; protected set; }

    public override bool HasActiveIntegrationObjects => IsValidConfiguration;

    // ISyncableConnection
    // Entra has no AzDO-style external system identifier (each connection is wired to a tenant
    // but the tenant id lives in Configuration, not at the system-id level). The interface
    // declares SystemId as nullable for exactly this case.
    public string? SystemId => null;

    public bool CanSync => IsActive && IsValidConfiguration && HasActiveIntegrationObjects;

    public Result Update(
        string name,
        string? description,
        string tenantId,
        string clientId,
        string clientSecret,
        string? allUsersGroupObjectId,
        bool includeDisabledUsers,
        Wayd.Common.Domain.Enums.AppIntegrations.EmployeeMatchProperty matchBy,
        bool normalizeNameCasing,
        bool configurationIsValid,
        Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));

            var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
            var newDescription = description?.NullIfWhiteSpacePlusTrim();
            var newTenantId = Guard.Against.NullOrWhiteSpace(tenantId, nameof(tenantId)).Trim();
            var newClientId = Guard.Against.NullOrWhiteSpace(clientId, nameof(clientId)).Trim();
            var newClientSecret = Guard.Against.NullOrWhiteSpace(clientSecret, nameof(clientSecret)).Trim();
            var newGroupId = string.IsNullOrWhiteSpace(allUsersGroupObjectId) ? null : allUsersGroupObjectId.Trim();

            if (!UpdateValuesChanged(newName, newDescription, newTenantId, newClientId, newClientSecret, newGroupId, includeDisabledUsers, matchBy, normalizeNameCasing, configurationIsValid))
                return Result.Success();

            Name = newName;
            Description = newDescription;
            IsValidConfiguration = configurationIsValid;

            Configuration.TenantId = newTenantId;
            Configuration.ClientId = newClientId;
            Configuration.ClientSecret = newClientSecret;
            Configuration.AllUsersGroupObjectId = newGroupId;
            Configuration.IncludeDisabledUsers = includeDisabledUsers;
            Configuration.MatchBy = matchBy;
            Configuration.NormalizeNameCasing = normalizeNameCasing;

            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    private bool UpdateValuesChanged(
        string name,
        string? description,
        string tenantId,
        string clientId,
        string clientSecret,
        string? allUsersGroupObjectId,
        bool includeDisabledUsers,
        Wayd.Common.Domain.Enums.AppIntegrations.EmployeeMatchProperty matchBy,
        bool normalizeNameCasing,
        bool configurationIsValid)
    {
        if (!string.Equals(Name, name, StringComparison.Ordinal)) return true;
        if (!string.Equals(Description, description, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.TenantId, tenantId, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.ClientId, clientId, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.ClientSecret, clientSecret, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.AllUsersGroupObjectId, allUsersGroupObjectId, StringComparison.Ordinal)) return true;
        if (Configuration.IncludeDisabledUsers != includeDisabledUsers) return true;
        if (Configuration.MatchBy != matchBy) return true;
        if (Configuration.NormalizeNameCasing != normalizeNameCasing) return true;
        if (IsValidConfiguration != configurationIsValid) return true;
        return false;
    }

    public static EntraConnection Create(
        string name,
        string? description,
        EntraConnectionConfiguration configuration,
        bool configurationIsValid,
        Instant timestamp)
    {
        var connection = new EntraConnection(name, description, configurationIsValid, configuration);

        connection.AddDomainEvent(EntityCreatedEvent.WithEntity(connection, timestamp));

        return connection;
    }
}
