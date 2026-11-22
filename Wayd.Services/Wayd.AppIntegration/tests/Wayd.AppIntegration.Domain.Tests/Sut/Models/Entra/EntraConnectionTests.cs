using FluentAssertions;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Domain.Tests.Sut.Models.Entra;

/// <summary>Holds the rule that the stored client secret is only sent for the tenant and app it was entered for.</summary>
public sealed class EntraConnectionTests
{
    private const string OriginalSecret = "original-secret";

    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString());

    private readonly TestingDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 02, 10, 12, 0, 0));

    [Theory]
    [InlineData("tenant-2", "client-1")]
    [InlineData("tenant-1", "client-2")]
    public void Update_RefusesToKeepTheSecret_WhenTheTenantOrClientChanges(string tenantId, string clientId)
    {
        // Arrange
        var connection = Create();
        var eventsBefore = connection.DomainEvents.Count;

        // Act
        var result = Update(connection, tenantId, clientId, clientSecret: null, name: "Renamed");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Connection.CredentialsRequiredMessage("client secret"));
        connection.Name.Should().Be("People");
        connection.Configuration.TenantId.Should().Be("tenant-1");
        connection.Configuration.ClientId.Should().Be("client-1");
        connection.DomainEvents.Should().HaveCount(eventsBefore);
    }

    [Fact]
    public void Update_ChangesTheTenant_WhenTheSecretIsEnteredAgain()
    {
        // Arrange
        var connection = Create();

        // Act
        var result = Update(connection, "tenant-2", "client-1", "new-secret");

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.TenantId.Should().Be("tenant-2");
        connection.Configuration.ClientSecret.Should().Be("new-secret");
    }

    [Fact]
    public void Update_KeepsTheSecret_WhenTheTenantAndClientAreUnchanged()
    {
        // Arrange
        var connection = Create();

        // Act
        var result = Update(connection, "tenant-1", "client-1", clientSecret: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.ClientSecret.Should().Be(OriginalSecret);
    }

    private EntraConnection Create() =>
        EntraConnection.Create("People", null, new EntraConnectionConfiguration("tenant-1", "client-1", OriginalSecret), true, Actor, _dateTimeProvider.Now);

    private CSharpFunctionalExtensions.Result Update(EntraConnection connection, string tenantId, string clientId, string? clientSecret, string name = "People") =>
        connection.Update(name, null, tenantId, clientId, clientSecret, null, false, EmployeeMatchProperty.Email, true, true, Actor, _dateTimeProvider.Now);
}
