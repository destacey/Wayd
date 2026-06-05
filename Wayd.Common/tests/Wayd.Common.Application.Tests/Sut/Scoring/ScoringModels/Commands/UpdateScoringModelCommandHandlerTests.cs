using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class UpdateScoringModelCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly ScoringModelFaker _faker = new();

    private UpdateScoringModelCommandHandler CreateHandler() =>
        new(_dbContext, NullLogger<UpdateScoringModelCommandHandler>.Instance);

    private ScoringModel SeedProposedModel()
    {
        var model = _faker.AsProposedWsjf();
        _dbContext.ScoringModels.Add(model);
        return model;
    }

    [Fact]
    public async Task Handle_ShouldChangeNameAndDescription()
    {
        // Arrange
        var model = SeedProposedModel();
        var command = new UpdateScoringModelCommand(model.Id, "Renamed", "New description.");

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        model.Name.Should().Be("Renamed");
        model.Description.Should().Be("New description.");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenModelNotFound()
    {
        // Arrange
        SeedProposedModel();
        var command = new UpdateScoringModelCommand(Guid.NewGuid(), "Renamed", "New description.");

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
