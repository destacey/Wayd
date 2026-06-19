using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class ArchiveScoringModelCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly ScoringModelFaker _faker = new();

    private ArchiveScoringModelCommandHandler CreateHandler() =>
        new(_dbContext, NullLogger<ArchiveScoringModelCommandHandler>.Instance);

    private ScoringModel SeedActiveModel()
    {
        var model = _faker.AsActiveWsjf();
        _dbContext.ScoringModels.Add(model);
        return model;
    }

    [Fact]
    public async Task Handle_ShouldTransitionActiveModelToArchived()
    {
        // Arrange
        var model = SeedActiveModel();
        var command = new ArchiveScoringModelCommand(model.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        model.State.Should().Be(ScoringModelState.Archived);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenModelNotFound()
    {
        // Arrange
        SeedActiveModel();
        var command = new ArchiveScoringModelCommand(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
