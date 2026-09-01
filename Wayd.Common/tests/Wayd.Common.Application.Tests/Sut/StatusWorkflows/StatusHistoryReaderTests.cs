using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.TestData.Core;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// Covers <see cref="StatusHistoryReader"/> — the read side of the status history.
/// </summary>
public sealed class StatusHistoryReaderTests : StatusWorkflowHandlerTestBase
{
    private static readonly Guid WorkflowId = Guid.CreateVersion7();

    private StatusHistoryReader CreateSut() => new(DbContext, DbContext);

    private static StatusRef Ref(string name, StatusCategory category, int alias = StatusWorkflow.NoAlias) =>
        new(WorkflowId, Guid.CreateVersion7(), name, category, alias);

    private static User TestUser(string id, string displayName) =>
        new PrivateConstructorFaker<User>()
            .RuleFor(u => u.Id, id)
            .RuleFor(u => u.DisplayName, displayName)
            .Generate();

    private Employee SeedEmployee(string firstName, string lastName)
    {
        var employee = new EmployeeFaker()
            .WithName(new PersonName(firstName, null, lastName))
            .Generate();

        DbContext.AddEmployee(employee);

        return employee;
    }

    /// <summary>
    /// A transition written directly, so the reader can be exercised without a save.
    /// </summary>
    private StatusTransition Transition(
        Guid recordId,
        int sequence,
        StatusRef to,
        StatusRef? from = null,
        EventActor? actor = null,
        string ownerType = "test.widget",
        string? reason = null,
        Instant? changedOn = null)
    {
        var transition = new StatusTransition(
            ownerType,
            recordId,
            from,
            to,
            actor ?? EventActor.System,
            changedOn ?? Now,
            sequence,
            reason);

        DbContext.AddStatusTransition(transition);

        return transition;
    }

    [Fact]
    public async Task Read_ReturnsTheHistoryNewestFirst()
    {
        // Arrange — appended oldest first, so an unordered read would come back in insertion order.
        var recordId = Guid.CreateVersion7();
        var proposed = Ref("Proposed", StatusCategory.Proposed);
        var notable = Ref("Notable", StatusCategory.Active, NotableAlias);
        var terminal = Ref("Terminal", StatusCategory.Done, TerminalAlias);

        Transition(recordId, 0, proposed);
        Transition(recordId, 1, notable, proposed);
        Transition(recordId, 2, terminal, notable);

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Sequence).Should().ContainInOrder(2, 1, 0);
        result.Value[0].ToStatus.Name.Should().Be("Terminal");
        result.Value[0].FromStatus!.Name.Should().Be("Notable");
    }

    [Fact]
    public async Task Read_ScopesToTheOwnerTypeAsWellAsTheRecord()
    {
        // Arrange — the same record id under two owner types. RecordId is not a foreign key and is only
        // unique within an owner type, so filtering on it alone would return both.
        var recordId = Guid.CreateVersion7();
        Transition(recordId, 0, Ref("Notable", StatusCategory.Active), ownerType: "test.widget");
        Transition(recordId, 0, Ref("Elsewhere", StatusCategory.Active), ownerType: "test.gadget");

        WorkflowOwners.Register(new WorkflowOwnerDescriptor(
            "test.gadget", "Gadget", new Dictionary<int, string>(), []));

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].ToStatus.Name.Should().Be("Notable");
    }

    [Fact]
    public async Task Read_ReportsTheNameFrozenOnTheTransitionNotTheCurrentOne()
    {
        // Arrange — the whole reason a status name is frozen onto the row rather than resolved.
        var recordId = Guid.CreateVersion7();
        Transition(recordId, 0, Ref("Under Review", StatusCategory.Proposed));

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert
        result.Value[0].ToStatus.Name.Should().Be("Under Review");
    }

    [Fact]
    public async Task Read_NamesTheActingUser()
    {
        // Arrange
        var recordId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7().ToString();
        DbContext.AddWaydUser(TestUser(userId, "Ada Lovelace"));
        Transition(recordId, 0, Ref("Notable", StatusCategory.Active), actor: EventActor.User(userId));

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert
        result.Value[0].ChangedByUser!.Name.Should().Be("Ada Lovelace");
        result.Value[0].ChangedBySystem.Should().BeFalse();
    }

    [Fact]
    public async Task Read_ReportsTheSystemActorAsTheSystem()
    {
        // Arrange
        var recordId = Guid.CreateVersion7();
        Transition(recordId, 0, Ref("Notable", StatusCategory.Active), actor: EventActor.System);

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert — resolved from the well-known id, so no user row is needed for it.
        result.Value[0].ChangedBySystem.Should().BeTrue();
        result.Value[0].ChangedByUser.Should().BeNull("the system is not an account a reader can visit");
    }

    [Fact]
    public async Task Read_KeepsATransitionWhoseAccountNoLongerExists()
    {
        // Arrange — no user row for this id. An inner join would drop the transition entirely, losing a
        // change that genuinely happened.
        var recordId = Guid.CreateVersion7();
        var deletedUserId = Guid.CreateVersion7().ToString();
        Transition(recordId, 0, Ref("Notable", StatusCategory.Active), actor: EventActor.User(deletedUserId));

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].ChangedByUser.Should().BeNull();
        result.Value[0].ChangedBySystem.Should().BeFalse("a deleted account is not the platform acting");
        result.Value[0].ChangedBy.Should().BeNull("no employee was frozen onto this transition");
    }

    [Fact]
    public async Task Read_NamesTheEmployeeFrozenOnTheTransition()
    {
        // Arrange — two employees, and two transitions attributed to different ones. A reader that
        // ignored the frozen id and simply took whichever employee it had loaded would pass with only
        // one in play, so both are seeded and each row is asserted against its own.
        var recordId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7().ToString();
        var ada = SeedEmployee("Ada", "Lovelace");
        var grace = SeedEmployee("Grace", "Hopper");
        DbContext.AddWaydUser(TestUser(userId, "ada.lovelace"));

        var proposed = Ref("Proposed", StatusCategory.Proposed);
        Transition(recordId, 0, proposed, actor: EventActor.User(userId, grace.Id));
        Transition(
            recordId, 1, Ref("Notable", StatusCategory.Active), from: proposed,
            actor: EventActor.User(userId, ada.Id));

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert — newest first, so [0] is Ada's move and [1] is Grace's.
        result.Value[0].ChangedBy!.Id.Should().Be(ada.Id);
        result.Value[0].ChangedBy!.Name.Should().Be("Ada Lovelace");
        result.Value[1].ChangedBy!.Id.Should().Be(grace.Id);
        result.Value[1].ChangedBy!.Name.Should().Be("Grace Hopper");
        result.Value[0].ChangedByUser!.Id.Should().Be(Guid.Parse(userId), "the account is reported alongside");
    }

    [Fact]
    public async Task Read_NamesAnImportsEmployeeEvenWithNoAccountBehindIt()
    {
        // Arrange — the case the frozen employee id exists for: an import knows who did the work, and
        // that person frequently has no account here at all.
        var recordId = Guid.CreateVersion7();
        var employee = SeedEmployee("Grace", "Hopper");
        Transition(
            recordId, 0, Ref("Notable", StatusCategory.Active),
            actor: EventActor.Import(originatingUserId: null, employeeId: employee.Id));

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert
        result.Value[0].ChangedBy!.Name.Should().Be("Grace Hopper");
        result.Value[0].ChangedByUser.Should().BeNull("nobody signed in to run this");
        result.Value[0].ChangedBySystem.Should().BeFalse();
    }

    [Fact]
    public async Task Read_ReturnsEmptyForARecordWithNoHistory()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Read_FailsForAnUnregisteredOwnerType()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.not-registered", Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a registered workflow owner type");
    }

    [Fact]
    public async Task Read_FailsForAnEmptyRecordId()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", Guid.Empty, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Read_CarriesTheReasonAndTheInitialTransitionsNullFromStatus()
    {
        // Arrange
        var recordId = Guid.CreateVersion7();
        Transition(recordId, 0, Ref("Proposed", StatusCategory.Proposed), reason: "Imported from the old tracker");

        var sut = CreateSut();

        // Act
        var result = await sut.Read("test.widget", recordId, TestContext.Current.CancellationToken);

        // Assert
        result.Value[0].FromStatus.Should().BeNull("a record entering its first status came from nowhere");
        result.Value[0].Reason.Should().Be("Imported from the old tracker");
    }
}
