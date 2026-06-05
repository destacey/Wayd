using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class DeleteScoringModelCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly ScoringModelFaker _faker = new();

    private DeleteScoringModelCommandHandler CreateHandler() =>
        new(_dbContext, NullLogger<DeleteScoringModelCommandHandler>.Instance);

    private ScoringModel SeedProposedModel()
    {
        var model = _faker.AsProposedWsjf();
        _dbContext.ScoringModels.Add(model);
        return model;
    }

    [Fact]
    public async Task Handle_ShouldRemoveProposedModel()
    {
        // Arrange
        var model = SeedProposedModel();
        var command = new DeleteScoringModelCommand(model.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (await _dbContext.ScoringModels.AnyAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenModelIsActive()
    {
        // Arrange — an active model cannot be deleted; the handler's CanBeDeleted guard should reject it
        // and leave the row in place.
        var model = _faker.AsActiveWsjf();
        _dbContext.ScoringModels.Add(model);
        var command = new DeleteScoringModelCommand(model.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be deleted");
        (await _dbContext.ScoringModels.AnyAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenModelNotFound()
    {
        // Arrange
        SeedProposedModel();
        var command = new DeleteScoringModelCommand(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
