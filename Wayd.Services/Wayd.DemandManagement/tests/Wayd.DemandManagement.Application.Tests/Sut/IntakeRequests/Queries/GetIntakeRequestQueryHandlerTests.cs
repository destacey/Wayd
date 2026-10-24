using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ForAnAdministratorWithNoLinkedEmployee_OffersNeitherEditNorReview()
    {
        // Arrange — every edit and review command needs a linked employee, so the flags must not offer them
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeRequestAuthorization.UpdatePermission,
            IntakeRequestAuthorization.ReviewPermission, IntakeRequestAuthorization.AdministerPermission);
        _scenario.Principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Substance.Should().NotBeNull();
        result.CanEdit.Should().BeFalse();
        result.CanReview.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForTheLinkedRequester_OffersEdit()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanEdit.Should().BeTrue();
        result.CanReview.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForARequestTheReaderCannotSee_ReturnsNull()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
