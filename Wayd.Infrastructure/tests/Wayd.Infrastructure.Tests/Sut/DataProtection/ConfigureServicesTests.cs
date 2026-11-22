using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Wayd.Infrastructure.Tests.Infrastructure;
using DataProtectionServices = Wayd.Infrastructure.DataProtection.ConfigureServices;

namespace Wayd.Infrastructure.Tests.Sut.DataProtection;

public sealed class ConfigureServicesTests
{
    private const string MasterKeyPath = "SecuritySettings:DataProtection:MasterKey";

    private static IConfiguration ConfigurationWithKey(string? masterKey)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [MasterKeyPath] = masterKey })
            .Build();

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ReadMasterKey_rejects_the_development_key_outside_development(string environmentName)
    {
        // Arrange
        var config = ApiConfigurationFiles.Load("security.Development.json");

        // Act
        var act = () => DataProtectionServices.ReadMasterKey(config, Environment(environmentName));

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*development-only key*");
    }

    [Fact]
    public void ReadMasterKey_accepts_the_development_key_in_development()
    {
        // Arrange
        var config = ApiConfigurationFiles.Load("security.Development.json");

        // Act
        var key = DataProtectionServices.ReadMasterKey(config, Environment(Environments.Development));

        // Assert
        key.Should().HaveCount(32);
    }

    [Fact]
    public void ReadMasterKey_finds_no_key_in_the_base_security_configuration()
    {
        // Arrange
        var config = ApiConfigurationFiles.Load("security.json");

        // Act
        var act = () => DataProtectionServices.ReadMasterKey(config, Environment(Environments.Production));

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Missing configuration section*");
    }

    [Fact]
    public void ReadMasterKey_accepts_a_generated_key_outside_development()
    {
        // Arrange
        var generated = RandomNumberGenerator.GetBytes(32);
        var config = ConfigurationWithKey(Convert.ToBase64String(generated));

        // Act
        var key = DataProtectionServices.ReadMasterKey(config, Environment(Environments.Production));

        // Assert
        key.Should().Equal(generated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("c2hvcnQ=")]
    public void ReadMasterKey_rejects_a_key_that_is_empty_malformed_or_the_wrong_length(string masterKey)
    {
        // Arrange
        var config = ConfigurationWithKey(masterKey);

        // Act
        var act = () => DataProtectionServices.ReadMasterKey(config, Environment(Environments.Development));

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{MasterKeyPath}*");
    }
}
