using FluentAssertions;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Domain.Tests.Sut.Models;

public sealed class ExternalSystemTests
{
    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString());

    private readonly TestingDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 02, 10, 12, 0, 0));

    [Fact]
    public void Create_RecordsTheSystemAndRaisesCreated()
    {
        // Arrange & Act
        var system = ExternalSystem.Create(Connector.AzureDevOps, " org-id ", " Contoso ", " https://dev.azure.com/contoso ", Actor, _dateTimeProvider.Now);

        // Assert
        system.Connector.Should().Be(Connector.AzureDevOps);
        system.SystemId.Should().Be("org-id");
        system.Name.Should().Be("Contoso");
        system.BaseUrl.Should().Be("https://dev.azure.com/contoso");

        var created = system.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalSystemCreatedEvent>().Subject;
        created.Id.Should().Be(system.Id);
        created.Connector.Should().Be(Connector.AzureDevOps);
        created.SystemId.Should().Be("org-id");
        created.Name.Should().Be("Contoso");
        created.BaseUrl.Should().Be("https://dev.azure.com/contoso");
    }

    [Fact]
    public void UpdateDetails_RaisesDetailsUpdatedWithBothEnds_WhenTheNameChanges()
    {
        // Arrange
        var system = ExternalSystem.Create(Connector.AzureDevOps, "org-id", "Contoso", "https://dev.azure.com/contoso", Actor, _dateTimeProvider.Now);

        // Act
        system.UpdateDetails("Fabrikam", "https://dev.azure.com/fabrikam", Actor, _dateTimeProvider.Now);

        // Assert
        system.Name.Should().Be("Fabrikam");
        system.BaseUrl.Should().Be("https://dev.azure.com/fabrikam");

        var updated = system.DomainEvents.OfType<ExternalSystemDetailsUpdatedEvent>().Should().ContainSingle().Subject;
        updated.Name.Should().Be("Fabrikam");
        updated.BaseUrl.Should().Be("https://dev.azure.com/fabrikam");
        updated.Previous.Should().Be(new ExternalSystemDetails("Contoso", "https://dev.azure.com/contoso"));
    }

    [Fact]
    public void UpdateDetails_RaisesNothing_WhenOnlyWhitespaceDiffers()
    {
        // Arrange
        var system = ExternalSystem.Create(Connector.Workday, "acme", "acme", "https://wd5-services1.myworkday.com", Actor, _dateTimeProvider.Now);

        // Act
        system.UpdateDetails(" acme ", "https://wd5-services1.myworkday.com ", Actor, _dateTimeProvider.Now);

        // Assert
        system.DomainEvents.OfType<ExternalSystemDetailsUpdatedEvent>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(Connector.Entra, "TENANT-1", true)]
    [InlineData(Connector.Entra, " tenant-1 ", true)]
    [InlineData(Connector.Entra, "tenant-2", false)]
    [InlineData(Connector.Workday, "tenant-1", false)]
    [InlineData(Connector.Entra, null, false)]
    public void Is_MatchesOnConnectorAndSystemIdIgnoringCase(Connector connector, string? systemId, bool expected)
    {
        // Arrange
        var system = ExternalSystem.Create(Connector.Entra, "tenant-1", "tenant-1", null, Actor, _dateTimeProvider.Now);

        // Act
        var result = system.Is(connector, systemId);

        // Assert
        result.Should().Be(expected);
    }
}
