using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Tests.Sut.Employees;

public sealed class EmployeeTests
{
    private const string PrimaryAddress = "avery.chen@acme.example";
    private const string FormerAddress = "avery.chen@acme-legacy.example";
    private const string ThirdAddress = "a.chen@acme.example";

    private static Employee CreateEmployee(string email = PrimaryAddress) =>
        new EmployeeFaker().WithEmail(new EmailAddress(email)).Generate();

    private static Employee CreateViaFactory(string email = PrimaryAddress, IEnumerable<(EmailAddress, bool)>? emails = null) =>
        Employee.Create(
            new PersonName("Avery", null, "Chen"),
            "E-4471",
            null,
            new EmailAddress(email),
            null,
            null,
            null,
            null,
            isActive: true,
            employeeType: null,
            SystemClock.Instance.GetCurrentInstant(),
            emails);

    [Fact]
    public void Create_ShouldSeedTheCollection_WithEmailAsPrimary()
    {
        // Arrange & Act
        var employee = CreateViaFactory();

        // Assert
        employee.Emails.Should().ContainSingle();
        employee.Emails.Single().Email.Value.Should().Be(PrimaryAddress);
        employee.Emails.Single().IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldSeedAdditionalAddresses_WhenSupplied()
    {
        // Arrange & Act
        var employee = CreateViaFactory(emails: [(new EmailAddress(FormerAddress), false)]);

        // Assert
        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo([PrimaryAddress, FormerAddress]);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(PrimaryAddress);
    }

    [Fact]
    public void Update_ShouldMovePrimary_WhenEmailChangesToAnAddressAlreadyInTheCollection()
    {
        // Arrange
        var employee = CreateViaFactory(emails: [(new EmailAddress(FormerAddress), false)]);

        // Act
        UpdateEmail(employee, FormerAddress);

        // Assert
        employee.Emails.Should().ContainSingle(e => e.IsPrimary);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(FormerAddress);
        employee.Emails.Single(e => e.Email.Value == PrimaryAddress).IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldAddAndFlagTheNewAddress_WhenEmailChangesToOneNotInTheCollection()
    {
        // Arrange
        var employee = CreateViaFactory();

        // Act
        UpdateEmail(employee, ThirdAddress);

        // Assert
        employee.Emails.Select(e => e.Email.Value).Should().Contain(ThirdAddress);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(ThirdAddress);
    }

    [Fact]
    public void Update_ShouldRetainTheFormerAddress_WhenEmailChanges()
    {
        // Arrange — the old address stays until a connector reconcile drops it.
        var employee = CreateViaFactory();

        // Act
        UpdateEmail(employee, ThirdAddress);

        // Assert
        employee.Emails.Select(e => e.Email.Value).Should().Contain(PrimaryAddress);
    }

    private static void UpdateEmail(Employee employee, string email) =>
        employee.Update(
            employee.Name,
            employee.EmployeeNumber,
            employee.HireDate,
            new EmailAddress(email),
            employee.JobTitle,
            employee.Department,
            employee.OfficeLocation,
            employee.ManagerId,
            employee.IsActive,
            employee.EmployeeType,
            SystemClock.Instance.GetCurrentInstant());

    [Fact]
    public void SyncEmails_ShouldAddAllReportedAddresses()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        employee.SyncEmails(
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(FormerAddress), false),
        ]);

        // Assert
        employee.Emails.Should().HaveCount(2);
        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo([PrimaryAddress, FormerAddress]);
    }

    [Fact]
    public void SyncEmails_ShouldRemoveAddressesTheSourceNoLongerReports()
    {
        // Arrange
        var employee = CreateEmployee();
        employee.SyncEmails(
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(FormerAddress), false),
        ]);

        // Act
        employee.SyncEmails([(new EmailAddress(PrimaryAddress), true)]);

        // Assert
        employee.Emails.Should().ContainSingle();
        employee.Emails.Single().Email.Value.Should().Be(PrimaryAddress);
    }

    [Fact]
    public void SyncEmails_ShouldRetainOnlyEmail_WhenSourceReportsNone()
    {
        // Arrange
        var employee = CreateEmployee();
        employee.SyncEmails(
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(FormerAddress), false),
        ]);

        // Act
        employee.SyncEmails([]);

        // Assert
        employee.Emails.Should().ContainSingle();
        employee.Emails.Single().Email.Value.Should().Be(PrimaryAddress);
        employee.Emails.Single().IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void SyncEmails_ShouldAddEmail_WhenSourceOmitsIt()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        employee.SyncEmails([(new EmailAddress(FormerAddress), true)]);

        // Assert
        employee.Emails.Select(e => e.Email.Value).Should().Contain(PrimaryAddress);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(PrimaryAddress);
    }

    [Fact]
    public void SyncEmails_ShouldIgnoreTheSourcePrimaryFlag()
    {
        // Arrange — a source flagging an address other than Email must not win.
        var employee = CreateEmployee();

        // Act
        employee.SyncEmails(
        [
            (new EmailAddress(PrimaryAddress), false),
            (new EmailAddress(FormerAddress), true),
        ]);

        // Assert
        employee.Emails.Should().ContainSingle(e => e.IsPrimary);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(PrimaryAddress);
    }

    [Fact]
    public void SyncEmails_ShouldFlagTheAddressMatchingEmail_AsPrimary()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        employee.SyncEmails(
        [
            (new EmailAddress(PrimaryAddress), false),
            (new EmailAddress(FormerAddress), false),
        ]);

        // Assert
        employee.Emails.Single(e => e.Email.Value == PrimaryAddress).IsPrimary.Should().BeTrue();
        employee.Emails.Single(e => e.Email.Value == FormerAddress).IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void SyncEmails_ShouldFlagExactlyOnePrimary_WhenSourceFlagsMultiple()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        employee.SyncEmails(
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(FormerAddress), true),
            (new EmailAddress(ThirdAddress), true),
        ]);

        // Assert
        employee.Emails.Should().ContainSingle(e => e.IsPrimary);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(PrimaryAddress);
    }

    [Fact]
    public void SyncEmails_ShouldDemoteFormerPrimary_WhenEmailChangesToAnotherReportedAddress()
    {
        // Arrange — the tenant-migration shape: today's primary becomes tomorrow's secondary.
        var employee = CreateEmployee(FormerAddress);
        employee.SyncEmails([(new EmailAddress(FormerAddress), true)]);

        // Act — Update sets the scalar first, then the connector reconciles.
        UpdateEmail(employee, PrimaryAddress);
        employee.SyncEmails(
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(FormerAddress), false),
        ]);

        // Assert
        employee.Emails.Should().ContainSingle(e => e.IsPrimary);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(PrimaryAddress);
        employee.Emails.Single(e => e.Email.Value == FormerAddress).IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void SyncEmails_ShouldKeepPrimaryConsistentWithEmail_WhenCalledBeforeTheScalarIsUpdated()
    {
        // Arrange — the reversed call order: reconcile runs against the not-yet-updated scalar.
        var employee = CreateEmployee(FormerAddress);

        // Act
        employee.SyncEmails([(new EmailAddress(PrimaryAddress), true)]);

        // Assert
        employee.Emails.Should().ContainSingle(e => e.IsPrimary);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(FormerAddress);
    }

    [Fact]
    public void SyncEmails_ShouldDeduplicate_IgnoringCase()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        employee.SyncEmails(
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(PrimaryAddress.ToUpperInvariant()), false),
        ]);

        // Assert
        employee.Emails.Should().ContainSingle();
    }

    [Fact]
    public void SyncEmails_ShouldNotReplaceRow_WhenOnlyCasingChanges()
    {
        // Arrange — a churned row would break PR 2's mapping, which keys on the address.
        var employee = CreateEmployee();
        employee.SyncEmails([(new EmailAddress(PrimaryAddress), true)]);
        var originalId = employee.Emails.Single().Id;

        // Act
        employee.SyncEmails([(new EmailAddress(PrimaryAddress.ToUpperInvariant()), true)]);

        // Assert
        employee.Emails.Single().Id.Should().Be(originalId);
    }

    [Fact]
    public void SyncEmails_ShouldBeIdempotent()
    {
        // Arrange
        var employee = CreateEmployee();
        (EmailAddress, bool)[] reported =
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(FormerAddress), false),
        ];
        employee.SyncEmails(reported);
        var originalIds = employee.Emails.Select(e => e.Id).ToArray();

        // Act
        employee.SyncEmails(reported);

        // Assert
        employee.Emails.Should().HaveCount(2);
        employee.Emails.Select(e => e.Id).Should().BeEquivalentTo(originalIds);
    }

    [Fact]
    public void SyncEmails_ShouldAssignEmployeeId_ToNewRows()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        employee.SyncEmails([(new EmailAddress(PrimaryAddress), true)]);

        // Assert
        employee.Emails.Single().EmployeeId.Should().Be(employee.Id);
    }

    [Fact]
    public void SyncEmails_ShouldReturnSuccess_WhenTheCollectionIsValid()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        var result = employee.SyncEmails([(new EmailAddress(FormerAddress), false)]);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SyncEmails_ShouldReturnFailure_WhenCollectionIsNull()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        var result = employee.SyncEmails(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SyncEmails_ShouldReturnFailure_AndLeaveTheCollectionIntact_WhenAnEntryIsNull()
    {
        // Arrange
        var employee = CreateEmployee();
        employee.SyncEmails([(new EmailAddress(FormerAddress), false)]);

        // Act
        var result = employee.SyncEmails([(new EmailAddress(ThirdAddress), false), (null!, false)]);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo([PrimaryAddress, FormerAddress]);
    }
}
