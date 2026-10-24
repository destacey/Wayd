using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class CreateIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private CreateIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    private static CreateIntakeRequestCommand Command(Guid deskId, Guid channelId, Guid? requesterId = null,
        IntakeRequestNeedByInput? needBy = null) =>
        new("Export invoices", "Finance needs a CSV export", "A day a month back", deskId, channelId, requesterId, needBy);

    [Fact]
    public async Task Handle_ForTheCaller_SubmitsToTheDeskWithTheCallerAsRequesterAndSubmitter()
    {
        // Arrange
        var caller = _scenario.Employee();
        _scenario.SignedInAs(caller);
        var desk = _scenario.Desk();
        var channel = _scenario.Channel();

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id, channel.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _scenario.DbContext.IntakeRequests.Single();
        created.RequesterId.Should().Be(caller.Id);
        created.SubmittedById.Should().Be(caller.Id);
        created.DeskId.Should().Be(desk.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnSomeonesBehalf_RecordsBothPeople()
    {
        // Arrange
        var caller = _scenario.Employee();
        var requester = _scenario.Employee();
        _scenario.SignedInAs(caller);

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, requester.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _scenario.DbContext.IntakeRequests.Single();
        created.RequesterId.Should().Be(requester.Id);
        created.SubmittedById.Should().Be(caller.Id);
    }

    [Fact]
    public async Task Handle_ToADeskRequestersCannotPick_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var desk = _scenario.Desk(isRequesterSelectable: false);

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id, _scenario.Channel().Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Choose a desk that requesters can submit to.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnInactiveRequester_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var requester = _scenario.Employee(isActive: false);

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, requester.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The requester must be an active employee.");
    }

    [Fact]
    public async Task Handle_ByACallerWhoseEmployeeIsInactive_Fails()
    {
        // Arrange — the submitter is named on the request too, so they must be active even on someone's behalf
        _scenario.SignedInAs(_scenario.Employee(isActive: false));
        var requester = _scenario.Employee();

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, requester.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only an active employee can submit a request.");
    }

    [Fact]
    public async Task Handle_WithACommitmentOnAnotherBasis_ReportsTheBasisRatherThanThePerson()
    {
        // Arrange — the shape of the need-by is checked before anyone it names is looked up
        _scenario.SignedInAs(_scenario.Employee());
        var needBy = new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.Regulatory, "SOX 404",
            Guid.NewGuid(), new LocalDate(2026, 9, 3));

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, needBy: needBy),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a commitment records who made it and when.");
    }

    [Fact]
    public async Task Handle_WithACommitmentBySomeoneUnknown_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var needBy = new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.Commitment, "Steering committee",
            Guid.NewGuid(), new LocalDate(2026, 9, 3));

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, needBy: needBy),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Whoever made the commitment must be an active employee.");
    }

    [Fact]
    public async Task Handle_WithAValidNeedBy_StoresItWithTheRequest()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var needBy = new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.Regulatory, "PCI DSS 4.0", null, null);

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, needBy: needBy),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.DbContext.IntakeRequests.Single().NeedBy!.Reference.Should().Be("PCI DSS 4.0");
    }
}
