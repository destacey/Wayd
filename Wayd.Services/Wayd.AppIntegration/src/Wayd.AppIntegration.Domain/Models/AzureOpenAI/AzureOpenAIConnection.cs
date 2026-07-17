using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models.AzureOpenAI;

public class AzureOpenAIConnection : Connection<AzureOpenAIConnectionConfiguration>
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private AzureOpenAIConnection() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private AzureOpenAIConnection(
        string name,
        string? description,
        bool configurationIsValid,
        AzureOpenAIConnectionConfiguration configuration)
    {
        Name = name;
        Description = description;
        IsValidConfiguration = configurationIsValid;
        Connector = Wayd.Common.Domain.Enums.AppIntegrations.Connector.AzureOpenAI;
        Configuration = Guard.Against.Null(configuration, nameof(Configuration));
    }

    public override AzureOpenAIConnectionConfiguration Configuration { get; protected set; }

    public override bool HasActiveIntegrationObjects => IsValidConfiguration;

    public Result Update(string name, string? description, string apiKey, string deploymentName, bool configurationIsValid, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));

            var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
            var newDescription = description?.NullIfWhiteSpacePlusTrim();
            var newApiKey = Guard.Against.NullOrWhiteSpace(apiKey, nameof(apiKey)).Trim();
            var newDeploymentName = Guard.Against.NullOrWhiteSpace(deploymentName, nameof(deploymentName)).Trim();

            if (!UpdateValuesChanged(newName, newDescription, newApiKey, newDeploymentName, configurationIsValid))
                return Result.Success();

            Name = newName;
            Description = newDescription;
            IsValidConfiguration = configurationIsValid;

            Configuration.ApiKey = newApiKey;
            Configuration.DeploymentName = newDeploymentName;

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
        string apiKey,
        string deploymentName,
        bool configurationIsValid)
    {
        return Name != name.Trim()
            || Description != description.NullIfWhiteSpacePlusTrim()
            || Configuration.ApiKey != apiKey.Trim()
            || Configuration.DeploymentName != deploymentName.Trim()
            || IsValidConfiguration != configurationIsValid; // Assuming AzureOpenAIConnectionConfiguration has an appropriate Equals method
    }

    public static AzureOpenAIConnection Create(
        string name,
        string? description,
        AzureOpenAIConnectionConfiguration configuration,
        bool configurationIsValid,
        Instant timestamp)
    {
        var connection = new AzureOpenAIConnection(
            name,
            description,
            configurationIsValid,
            configuration);

        return connection;
    }
}
