using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Common.Domain.Tests.Sut.Employees;

public sealed class EmployeeTests
{
    private const string PrimaryAddress = "avery.chen@acme.example";
    private const string FormerAddress = "avery.chen@acme-legacy.example";
    private const string ThirdAddress = "a.chen@acme.example";

    private static readonly Instant _now = Instant.FromUtc(2026, 8, 20, 12, 0, 0);
    private static readonly Guid _managerId = Guid.Parse("3d9a5f71-84c2-4e60-b1d7-6f2a0c94e5b8");
    private static readonly Guid _otherManagerId = Guid.Parse("7b1e4c28-9f03-4a56-8d72-0e5a3c96b1d4");

    private static Employee CreateEmployee(string email = PrimaryAddress) =>
        new EmployeeFaker().WithEmail(new EmailAddress(email)).Generate();

    private static Employee CreateViaFactory(string email = PrimaryAddress, IEnumerable<(EmailAddress, bool)>? emails = null, Guid? managerId = null) =>
        Employee.Create(
            new PersonName("Avery", null, "Chen"),
            "E-4471",
            null,
            new EmailAddress(email),
            null,
            null,
            null,
            managerId,
            isActive: true,
            employeeType: null,
            EventActor.System,
            _now,
            emails);

    /// <summary>An employee whose collection is already reconciled and whose events are cleared.</summary>
    private static Employee CreateSettledEmployee(Guid? managerId = null, bool isActive = true)
    {
        var employee = new EmployeeFaker()
            .WithEmail(new EmailAddress(PrimaryAddress))
            .WithManagerId(managerId)
            .WithIsActive(isActive)
            .Generate();
        employee.SyncEmails([(new EmailAddress(PrimaryAddress), true)], EventActor.System, _now);
        employee.ClearDomainEvents();

        return employee;
    }

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
    public void Create_ShouldNotRaiseCreatedEvent_UntilPostPersistenceActionsRun()
    {
        // Arrange & Act
        var employee = CreateViaFactory(managerId: _managerId);

        // Assert
        employee.DomainEvents.Should().BeEmpty();
        employee.PostPersistenceActions.Should().ContainSingle();
    }

    [Fact]
    public void Create_ShouldRaiseCreatedEvent_WithTheAssignedKey_WhenPostPersistenceActionsRun()
    {
        // Arrange
        var employee = CreateViaFactory(managerId: _managerId);
        employee.SetPrivate(e => e.Key, 42);

        // Act
        employee.ExecutePostPersistenceActions();

        // Assert
        var created = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeCreatedEvent>().Subject;
        created.Id.Should().Be(employee.Id);
        created.Key.Should().Be(42);
        created.ManagerId.Should().Be(_managerId);
        created.IsActive.Should().BeTrue();
        created.Actor.Should().Be(EventActor.System);
        created.Timestamp.Should().Be(_now);
    }

    [Fact]
    public void Create_ShouldRecordTheEmployeeAsCreated_WhenChangedBeforeTheFirstSave()
    {
        // Arrange — what the import does: link the manager and deactivate leavers before one save.
        var employee = CreateViaFactory(managerId: _managerId);

        // Act
        employee.UpdateManagerId(_otherManagerId, EventActor.System, _now);
        employee.Deactivate(EmployeeActivatableArgs.Create(EventActor.System, _now));
        employee.ExecutePostPersistenceActions();

        // Assert
        var created = employee.DomainEvents.OfType<EmployeeCreatedEvent>().Should().ContainSingle().Subject;
        created.ManagerId.Should().Be(_managerId);
        created.IsActive.Should().BeTrue("the deactivation is its own event");
        employee.DomainEvents.Should().ContainSingle(e => e is EmployeeManagerChangedEvent);
        employee.DomainEvents.Should().ContainSingle(e => e is EmployeeDeactivatedEvent);
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

    [Fact]
    public void Update_ShouldRaiseNothing_WhenNothingChanged()
    {
        // Arrange
        var employee = CreateSettledEmployee(_managerId);

        // Act — a sync sends every field on every run; the trimmed fields arrive padded.
        var result = employee.Update(
            employee.Name,
            $"  {employee.EmployeeNumber} ",
            employee.HireDate,
            new EmailAddress(PrimaryAddress),
            $" {employee.JobTitle}  ",
            $"{employee.Department} ",
            $" {employee.OfficeLocation}",
            employee.ManagerId,
            employee.IsActive,
            "   ",
            EventActor.System,
            _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_ShouldRaiseDetailsUpdated_WhenADescriptiveFieldChanges()
    {
        // Arrange
        var employee = CreateSettledEmployee(_managerId);

        // Act
        var result = UpdateWith(employee, jobTitle: "Principal Engineer");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeDetailsUpdatedEvent>().Subject;
        updated.Id.Should().Be(employee.Id);
        updated.Timestamp.Should().Be(_now);
        employee.JobTitle.Should().Be("Principal Engineer");
    }

    [Fact]
    public void Update_ShouldRaiseOnlyManagerChanged_WhenOnlyTheManagerChanges()
    {
        // Arrange
        var employee = CreateSettledEmployee(_managerId);

        // Act
        var result = UpdateWith(employee, managerId: _otherManagerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var changed = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeManagerChangedEvent>().Subject;
        changed.Id.Should().Be(employee.Id);
        changed.PreviousManagerId.Should().Be(_managerId);
        changed.ManagerId.Should().Be(_otherManagerId);
        employee.ManagerId.Should().Be(_otherManagerId);
    }

    [Fact]
    public void Update_ShouldRaiseDeactivated_WhenIsActiveBecomesFalse()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = UpdateWith(employee, isActive: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeDeactivatedEvent>()
            .Which.Id.Should().Be(employee.Id);
        employee.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldRaiseActivated_WhenIsActiveBecomesTrue()
    {
        // Arrange
        var employee = CreateSettledEmployee(isActive: false);

        // Act
        var result = UpdateWith(employee, isActive: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeActivatedEvent>()
            .Which.Id.Should().Be(employee.Id);
        employee.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UpdateManagerId_ShouldRaiseManagerChanged_WithBothEnds()
    {
        // Arrange
        var employee = CreateSettledEmployee(_managerId);

        // Act
        employee.UpdateManagerId(null, EventActor.System, _now);

        // Assert
        var changed = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeManagerChangedEvent>().Subject;
        changed.PreviousManagerId.Should().Be(_managerId);
        changed.ManagerId.Should().BeNull();
        changed.Timestamp.Should().Be(_now);
        employee.ManagerId.Should().BeNull();
    }

    [Fact]
    public void UpdateManagerId_ShouldRaiseNothing_WhenTheManagerIsUnchanged()
    {
        // Arrange
        var employee = CreateSettledEmployee(_managerId);

        // Act
        employee.UpdateManagerId(_managerId, EventActor.System, _now);

        // Assert
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Deactivate_ShouldRaiseDeactivated_WhenActive()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = employee.Deactivate(EmployeeActivatableArgs.Create(EventActor.System, _now));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deactivated = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeDeactivatedEvent>().Subject;
        deactivated.Id.Should().Be(employee.Id);
        deactivated.Timestamp.Should().Be(_now);
    }

    [Fact]
    public void Deactivate_ShouldRaiseNothing_WhenAlreadyInactive()
    {
        // Arrange
        var employee = CreateSettledEmployee(isActive: false);

        // Act
        var result = employee.Deactivate(EmployeeActivatableArgs.Create(EventActor.System, _now));

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_ShouldRaiseActivated_WhenInactive()
    {
        // Arrange
        var employee = CreateSettledEmployee(isActive: false);

        // Act
        var result = employee.Activate(EmployeeActivatableArgs.Create(EventActor.System, _now));

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeActivatedEvent>()
            .Which.Timestamp.Should().Be(_now);
    }

    [Fact]
    public void Activate_ShouldRaiseNothing_WhenAlreadyActive()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = employee.Activate(EmployeeActivatableArgs.Create(EventActor.System, _now));

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_ShouldRaiseDeleted_WithTheIdAndKey()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        employee.Delete(EventActor.System, _now);

        // Assert
        var deleted = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeDeletedEvent>().Subject;
        deleted.Id.Should().Be(employee.Id);
        deleted.Key.Should().Be(employee.Key);
        deleted.Timestamp.Should().Be(_now);
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
            EventActor.System,
            _now);

    private static CSharpFunctionalExtensions.Result UpdateWith(
        Employee employee,
        string? jobTitle = null,
        Guid? managerId = null,
        bool? isActive = null) =>
        employee.Update(
            employee.Name,
            employee.EmployeeNumber,
            employee.HireDate,
            employee.Email,
            jobTitle ?? employee.JobTitle,
            employee.Department,
            employee.OfficeLocation,
            managerId ?? employee.ManagerId,
            isActive ?? employee.IsActive,
            employee.EmployeeType,
            EventActor.System,
            _now);

    [Fact]
    public void SyncEmails_ShouldAddAllReportedAddresses()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        Sync(employee,
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
        Sync(employee,
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(FormerAddress), false),
        ]);

        // Act
        Sync(employee, [(new EmailAddress(PrimaryAddress), true)]);

        // Assert
        employee.Emails.Should().ContainSingle();
        employee.Emails.Single().Email.Value.Should().Be(PrimaryAddress);
    }

    [Fact]
    public void SyncEmails_ShouldRetainOnlyEmail_WhenSourceReportsNone()
    {
        // Arrange
        var employee = CreateEmployee();
        Sync(employee,
        [
            (new EmailAddress(PrimaryAddress), true),
            (new EmailAddress(FormerAddress), false),
        ]);

        // Act
        Sync(employee, []);

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
        Sync(employee, [(new EmailAddress(FormerAddress), true)]);

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
        Sync(employee,
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
        Sync(employee,
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
        Sync(employee,
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
        Sync(employee, [(new EmailAddress(FormerAddress), true)]);

        // Act — Update sets the scalar first, then the connector reconciles.
        UpdateEmail(employee, PrimaryAddress);
        Sync(employee,
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
        Sync(employee, [(new EmailAddress(PrimaryAddress), true)]);

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
        Sync(employee,
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
        Sync(employee, [(new EmailAddress(PrimaryAddress), true)]);
        var originalId = employee.Emails.Single().Id;

        // Act
        Sync(employee, [(new EmailAddress(PrimaryAddress.ToUpperInvariant()), true)]);

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
        Sync(employee, reported);
        var originalIds = employee.Emails.Select(e => e.Id).ToArray();

        // Act
        Sync(employee, reported);

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
        Sync(employee, [(new EmailAddress(PrimaryAddress), true)]);

        // Assert
        employee.Emails.Single().EmployeeId.Should().Be(employee.Id);
    }

    [Fact]
    public void SyncEmails_ShouldReturnSuccess_WhenTheCollectionIsValid()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        var result = Sync(employee, [(new EmailAddress(FormerAddress), false)]);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SyncEmails_ShouldReturnFailure_WhenCollectionIsNull()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        var result = Sync(employee, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SyncEmails_ShouldReturnFailure_AndLeaveTheCollectionIntact_WhenAnEntryIsNull()
    {
        // Arrange
        var employee = CreateEmployee();
        Sync(employee, [(new EmailAddress(FormerAddress), false)]);

        // Act
        var result = Sync(employee, [(new EmailAddress(ThirdAddress), false), (null!, false)]);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo([PrimaryAddress, FormerAddress]);
    }

    [Fact]
    public void SyncEmails_ShouldRaiseNothing_WhenTheSourceReportsTheSameSet()
    {
        // Arrange
        var employee = CreateSettledEmployee();
        Sync(employee, [(new EmailAddress(PrimaryAddress), true), (new EmailAddress(FormerAddress), false)]);
        employee.ClearDomainEvents();

        // Act
        Sync(employee, [(new EmailAddress(FormerAddress), false), (new EmailAddress(PrimaryAddress), true)]);

        // Assert
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SyncEmails_ShouldRaiseNothing_WhenOnlyCasingChanges()
    {
        // Arrange
        var employee = CreateSettledEmployee();
        Sync(employee, [(new EmailAddress(PrimaryAddress), true), (new EmailAddress(FormerAddress), false)]);
        employee.ClearDomainEvents();

        // Act
        Sync(employee,
        [
            (new EmailAddress(PrimaryAddress.ToUpperInvariant()), true),
            (new EmailAddress(FormerAddress.ToUpperInvariant()), false),
        ]);

        // Assert
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SyncEmails_ShouldRaiseWorkAddressesChanged_WhenAnAddressIsAdded()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        Sync(employee, [(new EmailAddress(PrimaryAddress), true), (new EmailAddress(FormerAddress), false)]);

        // Assert
        var changed = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeWorkAddressesChangedEvent>().Subject;
        changed.Id.Should().Be(employee.Id);
        changed.Timestamp.Should().Be(_now);
    }

    [Fact]
    public void SyncEmails_ShouldRaiseWorkAddressesChanged_WhenAnAddressIsRemoved()
    {
        // Arrange
        var employee = CreateSettledEmployee();
        Sync(employee, [(new EmailAddress(PrimaryAddress), true), (new EmailAddress(FormerAddress), false)]);
        employee.ClearDomainEvents();

        // Act
        Sync(employee, [(new EmailAddress(PrimaryAddress), true)]);

        // Assert
        employee.DomainEvents.Should().ContainSingle(e => e is EmployeeWorkAddressesChangedEvent);
    }

    [Fact]
    public void SyncEmails_ShouldRaiseNothing_WhenTheCollectionIsRejected()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = Sync(employee, [(new EmailAddress(ThirdAddress), false), (null!, false)]);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.DomainEvents.Should().BeEmpty();
    }

    private static CSharpFunctionalExtensions.Result Sync(Employee employee, IEnumerable<(EmailAddress, bool)> emails) =>
        employee.SyncEmails(emails, EventActor.System, _now);
}
