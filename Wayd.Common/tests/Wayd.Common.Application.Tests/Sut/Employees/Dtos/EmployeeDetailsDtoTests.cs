using FluentAssertions;
using Mapster;
using NodaTime;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Tests.Sut.Employees.Dtos;

public sealed class EmployeeDetailsDtoTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 12, 0);

    private static readonly TypeAdapterConfig Config = CreateConfig();

    private static TypeAdapterConfig CreateConfig()
    {
        var config = new TypeAdapterConfig();
        new EmployeeDetailsDto { DisplayName = "", FullName = "", FirstName = "", LastName = "", EmployeeNumber = "", Email = "" }
            .ConfigureMapping(config);
        new EmployeeListDto { DisplayName = "", FirstName = "", LastName = "", EmployeeNumber = "", Email = "" }
            .ConfigureMapping(config);
        return config;
    }

    [Fact]
    public void Map_ManagedEmployee_NamesItsConnector()
    {
        // Arrange
        var employee = Employee.CreateManaged(
            Connector.Workday, "acme", new PersonName("Avery", null, "Chen"), "E-1", null, new EmailAddress("avery@acme.example"),
            null, null, null, null, true, null, EventActor.System, Now);

        // Act
        var details = employee.Adapt<EmployeeDetailsDto>(Config);
        var listed = employee.Adapt<EmployeeListDto>(Config);

        // Assert
        details.Ownership.Should().Be(Ownership.Managed);
        details.Connector.Should().Be(Connector.Workday);
        details.AccessEndDate.Should().BeNull();
        listed.Ownership.Should().Be(Ownership.Managed);
    }

    [Fact]
    public void Map_OwnedEmployee_HasNoConnector_AndCarriesItsAccessEndDate()
    {
        // Arrange
        var accessEnd = new LocalDate(2027, 3, 31);
        var employee = Employee.Create(
            new PersonName("Riley", null, "Moss"), "ADV-1", null, new EmailAddress("riley@acme.example"),
            null, null, null, null, true, "Advisor", EventActor.System, Now, accessEndDate: accessEnd);

        // Act
        var details = employee.Adapt<EmployeeDetailsDto>(Config);
        var listed = employee.Adapt<EmployeeListDto>(Config);

        // Assert
        details.Ownership.Should().Be(Ownership.Owned);
        details.Connector.Should().BeNull();
        details.AccessEndDate.Should().Be(accessEnd);
        listed.Ownership.Should().Be(Ownership.Owned);
        listed.AccessEndDate.Should().Be(accessEnd);
    }
}
