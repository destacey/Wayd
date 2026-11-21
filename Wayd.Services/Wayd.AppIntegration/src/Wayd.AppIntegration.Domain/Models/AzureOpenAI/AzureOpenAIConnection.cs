using Wayd.Common.Domain.Events.AppIntegration;
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

    /// <summary>Applies an administrator's edit, refusing one that sends the kept key to another resource.</summary>
    /// <param name="apiKey">The new API key, or null to keep the stored one.</param>
    public Result Update(string name, string? description, string baseUrl, string? apiKey, string deploymentName, bool configurationIsValid, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(Configuration, nameof(Configuration));

        var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
        var newDescription = description?.NullIfWhiteSpacePlusTrim();
        var newBaseUrl = Guard.Against.NullOrWhiteSpace(baseUrl, nameof(baseUrl)).Trim();
        var newApiKey = apiKey is null
            ? Configuration.ApiKey
            : Guard.Against.NullOrWhiteSpace(apiKey, nameof(apiKey)).Trim();
        var newDeploymentName = Guard.Against.NullOrWhiteSpace(deploymentName, nameof(deploymentName)).Trim();

        if (!UpdateValuesChanged(newName, newDescription, newBaseUrl, newApiKey, newDeploymentName, configurationIsValid))
            return Result.Success();

        if (!AzureOpenAIConnectionConfiguration.IsValidBaseUrl(newBaseUrl))
            return Result.Failure($"BaseUrl '{newBaseUrl}' must be an absolute HTTPS URL.");

        var scopeResult = RequireCredentialsForNewScope([Authority(newBaseUrl)], apiKey is null, "API key");
        if (scopeResult.IsFailure)
            return scopeResult;

        var before = CaptureState();

        Name = newName;
        Description = newDescription;
        IsValidConfiguration = configurationIsValid;

        Configuration.BaseUrl = newBaseUrl;
        Configuration.ApiKey = newApiKey;
        Configuration.DeploymentName = newDeploymentName;

        RaiseChangesSince(before, actor, timestamp);

        return Result.Success();
    }

    private bool UpdateValuesChanged(
        string name,
        string? description,
        string baseUrl,
        string apiKey,
        string deploymentName,
        bool configurationIsValid)
    {
        return Name != name.Trim()
            || Description != description.NullIfWhiteSpacePlusTrim()
            || Configuration.BaseUrl != baseUrl
            || Configuration.ApiKey != apiKey.Trim()
            || Configuration.DeploymentName != deploymentName.Trim()
            || IsValidConfiguration != configurationIsValid;
    }

    public static AzureOpenAIConnection Create(
        string name,
        string? description,
        AzureOpenAIConnectionConfiguration configuration,
        bool configurationIsValid,
        EventActor actor,
        Instant timestamp)
    {
        // The API key is sent to BaseUrl; the command validator refuses anything else, so reaching this is a bug.
        Guard.Against.InvalidInput(configuration.BaseUrl, nameof(configuration), AzureOpenAIConnectionConfiguration.IsValidBaseUrl,
            "BaseUrl must be an absolute HTTPS URL.");

        var connection = new AzureOpenAIConnection(
            name,
            description,
            configurationIsValid,
            configuration);

        connection.RaiseCreated(actor, timestamp);

        return connection;
    }

    protected override ConnectionSetting[] DescribeSettings() =>
    [
        Setting(nameof(Configuration.BaseUrl), Configuration.BaseUrl),
        Setting(nameof(Configuration.DeploymentName), Configuration.DeploymentName),
        Setting(nameof(Configuration.DefaultTemperature), Configuration.DefaultTemperature),
        Setting(nameof(Configuration.DefaultMaxOutputTokens), Configuration.DefaultMaxOutputTokens),
        Setting(nameof(Configuration.JsonModePreferred), Configuration.JsonModePreferred),
    ];

    protected override IReadOnlyList<(string Name, string Value)> Credentials() =>
        [(nameof(Configuration.ApiKey), Configuration.ApiKey)];

    // The host with any non-default port: the deployment and path can change under the same key.
    protected override IReadOnlyList<string?> CredentialScope() => [Authority(Configuration.BaseUrl)];

    // A stored URL from before BaseUrl was validated may not parse; compared as it is, any edit to it is a new scope.
    private static string Authority(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Authority : baseUrl;
}
