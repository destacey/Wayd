using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class CreateIntakeDeskCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private CreateIntakeDeskCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    private static CreateIntakeDeskCommand Command(string name, params Guid[] reviewerIds) =>
        new(name, "Laptops, access and internal tools", IntakeDeskVisibility.SummaryOnly, true, true, true, reviewerIds);

    [Fact]
    public async Task Handle_WhenValid_CreatesTheDeskWithItsReviewers()
    {
        // Arrange
        var reviewer = _scenario.Employee();
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(Command(" IT ", reviewer.Id, reviewer.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var desk = _scenario.DbContext.IntakeDesks.Single();
        desk.Name.Should().Be("IT");
        desk.Visibility.Should().Be(IntakeDeskVisibility.SummaryOnly);
        desk.Reviewers.Select(r => r.EmployeeId).Should().Equal(reviewer.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithANameAlreadyTaken_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var existing = _scenario.Desk();

        // Act
        var result = await CreateHandler().Handle(Command(existing.Name), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A desk with this name already exists.");
    }

    [Fact]
    public async Task Handle_WithAnInactiveReviewer_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(Command("IT", _scenario.Employee(isActive: false).Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Every reviewer must be an active employee.");
    }
}
