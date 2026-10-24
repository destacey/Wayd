using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class DeleteIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private DeleteIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ForAVisibleRequest_RecordsTheDeletionAndRemovesIt()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestDeletedEvent>();
        _scenario.DbContext.IntakeRequests.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ForAPrivateRequestTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestCommand(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
        _scenario.DbContext.IntakeRequests.Should().ContainSingle();
    }
}
