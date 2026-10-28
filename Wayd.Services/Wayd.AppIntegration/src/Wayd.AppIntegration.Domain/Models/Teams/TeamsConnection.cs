using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models.Teams;

/// <summary>
/// A Microsoft Teams communication connection — stores Graph credentials; does not sync data.
/// </summary>
public sealed class TeamsConnection : Connection<TeamsConnectionConfiguration>
{
#pragma warning disable CS8618
    private TeamsConnection() { }
#pragma warning restore CS8618

    private TeamsConnection(
        string name,
        string? description,
        bool configurationIsValid,
        TeamsConnectionConfiguration configuration)
    {
        Name = name;
        Description = description;
        IsValidConfiguration = configurationIsValid;
        Connector = Wayd.Common.Domain.Enums.AppIntegrations.Connector.Teams;
        Configuration = Guard.Against.Null(configuration, nameof(Configuration));
    }

    public override TeamsConnectionConfiguration Configuration { get; protected set; }

    public override bool HasActiveIntegrationObjects => IsValidConfiguration;

    public Result Update(
        string name,
        string? description,
        string tenantId,
        string clientId,
        string clientSecret,
        bool configurationIsValid,
        EventActor actor,
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

            if (!UpdateValuesChanged(newName, newDescription, newTenantId, newClientId, newClientSecret, configurationIsValid))
                return Result.Success();

            var before = CaptureState();

            Name = newName;
            Description = newDescription;
            IsValidConfiguration = configurationIsValid;
            Configuration.TenantId = newTenantId;
            Configuration.ClientId = newClientId;
            Configuration.ClientSecret = newClientSecret;

            RaiseChangesSince(before, actor, timestamp);

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
        bool configurationIsValid)
    {
        if (!string.Equals(Name, name, StringComparison.Ordinal)) return true;
        if (!string.Equals(Description, description, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.TenantId, tenantId, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.ClientId, clientId, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.ClientSecret, clientSecret, StringComparison.Ordinal)) return true;
        if (IsValidConfiguration != configurationIsValid) return true;
        return false;
    }

    public static TeamsConnection Create(
        string name,
        string? description,
        TeamsConnectionConfiguration configuration,
        bool configurationIsValid,
        EventActor actor,
        Instant timestamp)
    {
        var connection = new TeamsConnection(name, description, configurationIsValid, configuration);
        connection.RaiseCreated(actor, timestamp);
        return connection;
    }

    protected override ConnectionSetting[] DescribeSettings() =>
    [
        Setting(nameof(Configuration.TenantId), Configuration.TenantId),
        Setting(nameof(Configuration.ClientId), Configuration.ClientId),
    ];

    protected override IReadOnlyList<(string Name, string Value)> Credentials() =>
        [(nameof(Configuration.ClientSecret), Configuration.ClientSecret)];
}
