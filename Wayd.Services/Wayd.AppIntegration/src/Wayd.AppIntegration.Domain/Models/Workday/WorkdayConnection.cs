using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models.Workday;

public sealed class WorkdayConnection : Connection<WorkdayConnectionConfiguration>, ISyncableConnection
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private WorkdayConnection() { }
#pragma warning restore CS8618

    private WorkdayConnection(
        string name,
        string? description,
        bool configurationIsValid,
        WorkdayConnectionConfiguration configuration)
    {
        Name = name;
        Description = description;
        IsValidConfiguration = configurationIsValid;
        Connector = Wayd.Common.Domain.Enums.AppIntegrations.Connector.Workday;
        Configuration = Guard.Against.Null(configuration, nameof(Configuration));
    }

    public override WorkdayConnectionConfiguration Configuration { get; protected set; }

    public override bool HasActiveIntegrationObjects => IsValidConfiguration;

    public string? SystemId => null;

    public bool CanSync => IsActive && IsValidConfiguration && HasActiveIntegrationObjects;

    public Result Update(
        string name,
        string? description,
        string wsdlUrl,
        string isuUsername,
        string isuPassword,
        WorkdayWorkerKey workerKey,
        bool includeInactive,
        bool incrementalSyncEnabled,
        EmployeeMatchProperty matchBy,
        bool configurationIsValid,
        Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));

            var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
            var newDescription = description?.NullIfWhiteSpacePlusTrim();
            var newWsdlUrl = Guard.Against.NullOrWhiteSpace(wsdlUrl, nameof(wsdlUrl)).Trim();
            var newIsuUsername = Guard.Against.NullOrWhiteSpace(isuUsername, nameof(isuUsername)).Trim();
            var newIsuPassword = Guard.Against.NullOrWhiteSpace(isuPassword, nameof(isuPassword)).Trim();

            if (!UpdateValuesChanged(newName, newDescription, newWsdlUrl, newIsuUsername, newIsuPassword, workerKey, includeInactive, incrementalSyncEnabled, matchBy, configurationIsValid))
                return Result.Success();

            Name = newName;
            Description = newDescription;
            IsValidConfiguration = configurationIsValid;

            Configuration.WsdlUrl = newWsdlUrl;
            Configuration.IsuUsername = newIsuUsername;
            Configuration.IsuPassword = newIsuPassword;
            Configuration.WorkerKey = workerKey;
            Configuration.IncludeInactive = includeInactive;
            Configuration.IncrementalSyncEnabled = incrementalSyncEnabled;
            Configuration.MatchBy = matchBy;

            // Re-derive endpoint parts from the (possibly new) URL. A bad URL leaves them blank;
            // the init probe that follows the save catches that and flips IsValidConfiguration.
            if (WorkdayConnectionConfiguration.TryParse(newWsdlUrl, out var parts))
            {
                Configuration.ServiceHost = parts.ServiceHost;
                Configuration.TenantAlias = parts.TenantAlias;
                Configuration.WsdlVersion = parts.WsdlVersion;
                Configuration.SoapEndpoint = parts.SoapEndpoint;
            }
            else
            {
                Configuration.ServiceHost = string.Empty;
                Configuration.TenantAlias = string.Empty;
                Configuration.WsdlVersion = string.Empty;
                Configuration.SoapEndpoint = string.Empty;
            }

            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>
    /// Records the structured result of an init probe (called by Create/Update/Init handlers).
    /// Drives <see cref="Connection.IsValidConfiguration"/>.
    /// </summary>
    public void RecordInitResult(
        bool succeeded,
        IReadOnlyList<string>? missingFields,
        IReadOnlyList<string>? warnings,
        string? authError,
        DateTimeOffset now)
    {
        Guard.Against.Null(Configuration, nameof(Configuration));

        Configuration.LastInitAt = now;
        Configuration.LastInitSucceeded = succeeded;
        Configuration.LastInitMissingFields = missingFields?.ToList();
        Configuration.LastInitWarnings = warnings?.ToList();
        Configuration.LastInitAuthError = authError;

        IsValidConfiguration = succeeded;
    }

    private bool UpdateValuesChanged(
        string name,
        string? description,
        string wsdlUrl,
        string isuUsername,
        string isuPassword,
        WorkdayWorkerKey workerKey,
        bool includeInactive,
        bool incrementalSyncEnabled,
        EmployeeMatchProperty matchBy,
        bool configurationIsValid)
    {
        if (!string.Equals(Name, name, StringComparison.Ordinal)) return true;
        if (!string.Equals(Description, description, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.WsdlUrl, wsdlUrl, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.IsuUsername, isuUsername, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.IsuPassword, isuPassword, StringComparison.Ordinal)) return true;
        if (Configuration.WorkerKey != workerKey) return true;
        if (Configuration.IncludeInactive != includeInactive) return true;
        if (Configuration.IncrementalSyncEnabled != incrementalSyncEnabled) return true;
        if (Configuration.MatchBy != matchBy) return true;
        if (IsValidConfiguration != configurationIsValid) return true;
        return false;
    }

    public static WorkdayConnection Create(
        string name,
        string? description,
        WorkdayConnectionConfiguration configuration,
        bool configurationIsValid,
        Instant timestamp)
    {
        var connection = new WorkdayConnection(name, description, configurationIsValid, configuration);

        connection.AddDomainEvent(EntityCreatedEvent.WithEntity(connection, timestamp));

        return connection;
    }
}
