using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class AddScoringModelCriterionCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly ScoringModelFaker _faker = new();

    private AddScoringModelCriterionCommandHandler CreateHandler() =>
        new(_dbContext, NullLogger<AddScoringModelCriterionCommandHandler>.Instance);

    // A bare proposed model with no criteria yet, so the command under test does the adding.
    private ScoringModel SeedEmptyModel()
    {
        var model = _faker.AsProposedWith([], [], []);
        _dbContext.ScoringModels.Add(model);
        return model;
    }

    [Fact]
    public async Task Handle_ShouldAddCriterionAndReturnItsId()
    {
        // Arrange
        var model = SeedEmptyModel();
        var command = new AddScoringModelCriterionCommand(
            model.Id, "Business Value", "BV", "Value to the business.", null, ScaleId: null);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var criterion = model.Criteria.Single();
        criterion.Id.Should().Be(result.Value);
        criterion.Token.Should().Be("BV");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTokenDuplicatesExistingCriterion()
    {
        // Arrange — the model already has a "BV" criterion; adding another must surface the domain
        // failure and not save.
        var model = _faker.AsProposedWsjf();
        _dbContext.ScoringModels.Add(model);
        var command = new AddScoringModelCriterionCommand(
            model.Id, "Duplicate", "BV", null, null, ScaleId: null);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenModelNotFound()
    {
        // Arrange
        SeedEmptyModel();
        var command = new AddScoringModelCriterionCommand(
            Guid.NewGuid(), "Business Value", "BV", null, null, ScaleId: null);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
