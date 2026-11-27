using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Models;
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

    private static readonly LocalDate Today = new(2026, 8, 20);

    private static CSharpFunctionalExtensions.Result ExtendAccess(Employee employee, LocalDate accessEndDate) =>
        employee.Update(
            employee.Name, employee.EmployeeNumber, employee.HireDate, employee.Email, employee.JobTitle,
            employee.Department, employee.OfficeLocation, employee.ManagerId, employee.EmployeeType, accessEndDate,
            Today, EventActor.System, _now);

    [Fact]
    public void Update_ShouldReactivate_WhenAnEndedAccessEndDateMovesToTodayOrLater()
    {
        // Arrange — deactivated because its access had ended
        var employee = new EmployeeFaker().WithAccessEndDate(Today.PlusDays(-3)).WithIsActive(false).Generate();

        // Act
        var result = ExtendAccess(employee, Today);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.IsActive.Should().BeTrue();
        employee.DomainEvents.OfType<EmployeeActivatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Update_ShouldNotReactivate_WhenTheAccessEndDateHadNotPassed()
    {
        // Arrange — inactive for another reason, such as an imported leaver whose date is still ahead
        var employee = new EmployeeFaker().WithAccessEndDate(Today.PlusDays(10)).WithIsActive(false).Generate();

        // Act
        ExtendAccess(employee, Today.PlusDays(30));

        // Assert
        employee.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldNotReactivate_WhenTheNewAccessEndDateHasAlsoPassed()
    {
        // Arrange
        var employee = new EmployeeFaker().WithAccessEndDate(Today.PlusDays(-30)).WithIsActive(false).Generate();

        // Act
        ExtendAccess(employee, Today.PlusDays(-1));

        // Assert
        employee.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Adopt_ShouldFail_WhenTheEmployeeNumberIsTooLongToBeAnExternalId()
    {
        // Arrange — only an employee that predates the 64-character limit can have one
        var employee = new EmployeeFaker().WithEmployeeNumber(new string('x', Employee.ExternalReferenceMaxLength + 1)).Generate();

        // Act
        var result = employee.Adopt(SyncConnector, SyncSystemId, EventActor.System, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.IsManaged.Should().BeFalse();
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TakeOver_ShouldFail_WhenTheSystemIdIsTooLong()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = employee.TakeOver(Connector.Entra, new string('t', Employee.ExternalReferenceMaxLength + 1), EventActor.System, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
    }

    private const Connector SyncConnector = Connector.Workday;
    private const string SyncSystemId = "acme";
    private const string SettledNumber = "E-4471";

    /// <summary>
    /// A people sync's employee whose collection is already reconciled and whose events are cleared.
    /// </summary>
    private static Employee CreateSettledEmployee(Guid? managerId = null, bool isActive = true)
    {
        var employee = new EmployeeFaker()
            .WithEmail(new EmailAddress(PrimaryAddress))
            .WithEmployeeNumber(SettledNumber)
            .WithOwnershipInfo(OwnershipInfo.CreateExternalOwned(SyncConnector, SyncSystemId, SettledNumber))
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
    public void UpdateFromSource_ShouldRaiseNothing_WhenNothingChanged()
    {
        // Arrange
        var employee = CreateSettledEmployee(_managerId);

        // Act — a sync sends every field on every run; the trimmed fields arrive padded.
        var result = employee.UpdateFromSource(
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
    public void UpdateFromSource_ShouldRaiseDetailsUpdated_WhenADescriptiveFieldChanges()
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
    public void UpdateFromSource_ShouldRaiseOnlyManagerChanged_WhenOnlyTheManagerChanges()
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
    public void UpdateFromSource_ShouldRaiseDeactivated_WhenIsActiveBecomesFalse()
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
    public void UpdateFromSource_ShouldRaiseActivated_WhenIsActiveBecomesTrue()
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

    [Fact]
    public void Create_ShouldBeOwnedByWayd_WithTheAccessEndDate()
    {
        // Arrange
        var accessEnd = new LocalDate(2027, 3, 31);

        // Act
        var employee = Employee.Create(
            new PersonName("Avery", null, "Chen"), "E-4471", null, new EmailAddress(PrimaryAddress),
            null, null, null, null, isActive: true, employeeType: "Advisor", EventActor.System, _now,
            accessEndDate: accessEnd);

        // Assert
        employee.OwnershipInfo.Ownership.Should().Be(Ownership.Owned);
        employee.OwnershipInfo.HasExternalReference.Should().BeFalse();
        employee.IsManaged.Should().BeFalse();
        employee.AccessEndDate.Should().Be(accessEnd);
    }

    [Fact]
    public void CreateManaged_ShouldBeManagedByItsSystem_WithTheNumberAsExternalId()
    {
        // Act
        var employee = Employee.CreateManaged(
            SyncConnector, SyncSystemId, new PersonName("Avery", null, "Chen"), " E-4471 ", null,
            new EmailAddress(PrimaryAddress), null, null, null, null, isActive: true, employeeType: null,
            EventActor.System, _now);

        // Assert
        employee.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
        employee.OwnershipInfo.ExternalId.Should().Be("E-4471");
        employee.AccessEndDate.Should().BeNull();
    }

    [Fact]
    public void IsManagedBy_ShouldBeFalse_ForAnotherInstanceOfTheSameKindOfSystem()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act & Assert
        employee.IsManagedBy(SyncConnector, "other-tenant").Should().BeFalse();
        employee.IsManagedBy(Connector.Entra, SyncSystemId).Should().BeFalse();
        employee.IsManagedBy(SyncConnector, SyncSystemId.ToUpperInvariant()).Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldFail_WhenAPeopleSyncManagesTheEmployee()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = employee.Update(
            employee.Name, employee.EmployeeNumber, employee.HireDate, employee.Email, "Principal Engineer",
            employee.Department, employee.OfficeLocation, employee.ManagerId, employee.EmployeeType,
            new LocalDate(2027, 1, 1), Today, EventActor.System, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.JobTitle.Should().NotBe("Principal Engineer");
        employee.AccessEndDate.Should().BeNull();
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_ShouldChangeTheAccessEndDate_AndRaiseDetailsUpdated()
    {
        // Arrange
        var employee = CreateEmployee();
        var accessEnd = new LocalDate(2027, 6, 30);

        // Act
        var result = employee.Update(
            employee.Name, employee.EmployeeNumber, employee.HireDate, employee.Email, employee.JobTitle,
            employee.Department, employee.OfficeLocation, employee.ManagerId, "Advisor", accessEnd,
            Today, EventActor.System, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.AccessEndDate.Should().Be(accessEnd);
        employee.EmployeeType.Should().Be("Advisor");
        employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeDetailsUpdatedEvent>();
    }

    [Fact]
    public void UpdateFromSource_ShouldFail_WhenWaydOwnsTheEmployee()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        var result = UpdateWith(employee, jobTitle: "Principal Engineer");

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.JobTitle.Should().NotBe("Principal Engineer");
    }

    [Fact]
    public void UpdateFromSource_ShouldMoveTheExternalId_WhenTheSourceRenumbersTheEmployee()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = UpdateWith(employee, employeeNumber: "W-0091");

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.EmployeeNumber.Should().Be("W-0091");
        employee.OwnershipInfo.ExternalId.Should().Be("W-0091");
        employee.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
    }

    [Fact]
    public void Adopt_ShouldHandTheEmployeeToTheSync_AndRaiseAdoptedWithBothEnds()
    {
        // Arrange
        var accessEnd = new LocalDate(2027, 3, 31);
        var employee = new EmployeeFaker().WithEmployeeNumber("E-4471").WithAccessEndDate(accessEnd).Generate();

        // Act
        var result = employee.Adopt(SyncConnector, SyncSystemId, EventActor.System, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
        employee.OwnershipInfo.ExternalId.Should().Be("E-4471");
        employee.AccessEndDate.Should().BeNull("the people source decides when a managed employee's access ends");

        var adopted = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeAdoptedEvent>().Subject;
        adopted.Id.Should().Be(employee.Id);
        adopted.PreviousOwnership.Should().Be(Ownership.Owned);
        adopted.PreviousAccessEndDate.Should().Be(accessEnd);
        adopted.Ownership.Should().Be(Ownership.Managed);
        adopted.Connector.Should().Be(SyncConnector);
        adopted.SystemId.Should().Be(SyncSystemId);
        adopted.Timestamp.Should().Be(_now);
    }

    [Fact]
    public void Adopt_ShouldFail_WhenAPeopleSyncAlreadyManagesTheEmployee()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = employee.Adopt(Connector.Entra, "tenant", EventActor.System, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.IsManagedBy(SyncConnector, SyncSystemId).Should().BeTrue();
        employee.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TakeOver_ShouldMoveTheEmployeeToTheNewSystem_AndRaiseTakenOverWithBothEnds()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = employee.TakeOver(Connector.Entra, "tenant-2", EventActor.System, _now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        employee.IsManagedBy(Connector.Entra, "tenant-2").Should().BeTrue();
        employee.OwnershipInfo.ExternalId.Should().Be(SettledNumber);

        var takenOver = employee.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeTakenOverEvent>().Subject;
        takenOver.PreviousConnector.Should().Be(SyncConnector);
        takenOver.PreviousSystemId.Should().Be(SyncSystemId);
        takenOver.Connector.Should().Be(Connector.Entra);
        takenOver.SystemId.Should().Be("tenant-2");
    }

    [Fact]
    public void TakeOver_ShouldFail_WhenWaydOwnsTheEmployee()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act
        var result = employee.TakeOver(SyncConnector, SyncSystemId, EventActor.System, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.IsManaged.Should().BeFalse();
    }

    [Fact]
    public void TakeOver_ShouldFail_WhenTheSystemAlreadyManagesTheEmployee()
    {
        // Arrange
        var employee = CreateSettledEmployee();

        // Act
        var result = employee.TakeOver(SyncConnector, SyncSystemId, EventActor.System, _now);

        // Assert
        result.IsFailure.Should().BeTrue();
        employee.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(2027, 3, 30, false)]
    [InlineData(2027, 3, 31, false)]
    [InlineData(2027, 4, 1, true)]
    public void HasAccessEnded_ShouldBeTrue_OnlyAfterTheLastDay(int year, int month, int day, bool expected)
    {
        // Arrange
        var employee = new EmployeeFaker().WithAccessEndDate(new LocalDate(2027, 3, 31)).Generate();

        // Act & Assert
        employee.HasAccessEnded(new LocalDate(year, month, day)).Should().Be(expected);
    }

    [Fact]
    public void HasAccessEnded_ShouldBeFalse_WithoutAnAccessEndDate()
    {
        // Arrange
        var employee = CreateEmployee();

        // Act & Assert
        employee.HasAccessEnded(new LocalDate(2099, 1, 1)).Should().BeFalse();
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
            employee.EmployeeType,
            employee.AccessEndDate,
            Today,
            EventActor.System,
            _now);

    private static CSharpFunctionalExtensions.Result UpdateWith(
        Employee employee,
        string? jobTitle = null,
        Guid? managerId = null,
        bool? isActive = null,
        string? employeeNumber = null) =>
        employee.UpdateFromSource(
            employee.Name,
            employeeNumber ?? employee.EmployeeNumber,
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
