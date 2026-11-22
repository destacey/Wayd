using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Wayd.Infrastructure.Tests.Infrastructure;
using LocalJwtServices = Wayd.Infrastructure.Auth.Local.ConfigureServices;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Local;

public sealed class ConfigureServicesTests
{
    private const string SecretPath = "SecuritySettings:LocalJwt:Secret";

    private static IConfiguration ConfigurationWithSecret(string? secret)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SecretPath] = secret,
                ["SecuritySettings:LocalJwt:Issuer"] = "https://wayd.dev",
                ["SecuritySettings:LocalJwt:Audience"] = "https://api.wayd.dev",
            })
            .Build();

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void ReadSettings_rejects_a_secret_shorter_than_the_minimum(string environmentName)
    {
        // Arrange
        var config = ConfigurationWithSecret(new string('x', LocalJwtServices.MinimumSecretLength - 1));

        // Act
        var act = () => LocalJwtServices.ReadSettings(config, Environment(environmentName));

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*at least 32 characters*");
    }

    [Fact]
    public void ReadSettings_accepts_a_secret_of_the_minimum_length()
    {
        // Arrange
        var secret = new string('x', LocalJwtServices.MinimumSecretLength);
        var config = ConfigurationWithSecret(secret);

        // Act
        var settings = LocalJwtServices.ReadSettings(config, Environment(Environments.Production));

        // Assert
        settings.Secret.Should().Be(secret);
    }

    [Fact]
    public void ReadSettings_rejects_the_development_secret_outside_development()
    {
        // Arrange
        var config = ApiConfigurationFiles.Load("security.Development.json");

        // Act
        var act = () => LocalJwtServices.ReadSettings(config, Environment(Environments.Production));

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*development-only placeholder*");
    }

    [Fact]
    public void ReadSettings_accepts_the_development_secret_in_development()
    {
        // Arrange
        var config = ApiConfigurationFiles.Load("security.Development.json");

        // Act
        var act = () => LocalJwtServices.ReadSettings(config, Environment(Environments.Development));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ReadSettings_finds_no_secret_in_the_base_security_configuration()
    {
        // Arrange
        var config = ApiConfigurationFiles.Load("security.json");

        // Act
        var act = () => LocalJwtServices.ReadSettings(config, Environment(Environments.Production));

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*must be configured*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void ReadSettings_rejects_a_missing_secret(string? secret)
    {
        // Arrange
        var config = ConfigurationWithSecret(secret);

        // Act
        var act = () => LocalJwtServices.ReadSettings(config, Environment(Environments.Development));

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*must be configured*");
    }
}
