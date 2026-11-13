using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Queries;

public sealed class GetIntakeDeskOpenEstimatesQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeDeskOpenEstimatesQueryHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ByADeskAdmin_CountsOpenEstimatesByValueAndVersion()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var older = _scenario.EstimationScale("Fibonacci");
        var current = _scenario.EstimationScale("T-Shirt Sizes");
        var desk = _scenario.EstimatesOn(_scenario.DeskAdministeredBy(deskAdmin.Id), current);
        _scenario.Estimated(_scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Active), desk, "L");
        _scenario.Estimated(_scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Proposed), desk, "L");
        _scenario.Estimated(_scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Active), desk, "S");
        _scenario.DbContext.AddRequests(
            new IntakeRequestFaker().WithDeskId(desk.Id).InStatus(StatusCategory.Active).WithEstimate(older, "M").Generate(),
            new IntakeRequestFaker().WithDeskId(desk.Id).InStatus(StatusCategory.Done).WithEstimate(current, "XL").Generate(),
            new IntakeRequestFaker().WithDeskId(desk.Id).InStatus(StatusCategory.Removed).WithEstimate(current, "XL").Generate());
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskOpenEstimatesQuery(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Select(c => (c.Scale.Name, c.Value, c.RequestCount))
            .Should().Equal(("Fibonacci", "M", 1), ("T-Shirt Sizes", "S", 1), ("T-Shirt Sizes", "L", 2));
        result.Value.Should().OnlyContain(c => c.Scale.IsCurrentVersion);
    }

    [Fact]
    public async Task Handle_ForADeskWithNoOpenEstimate_ReturnsNone()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.EstimationScale());
        _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Active);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskOpenEstimatesQuery(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdminOffTheDesk_Answers()
    {
        // Arrange
        var desk = _scenario.EstimatesOn(_scenario.Desk(), _scenario.EstimationScale());
        _scenario.Estimated(_scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Active), desk, "M");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskOpenEstimatesQuery(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().ContainSingle().Which.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAnAgent_Fails()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.DeskWithMembers(new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent));
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskOpenEstimatesQuery(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotADeskAdminError);
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReportsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskOpenEstimatesQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
    }
}
