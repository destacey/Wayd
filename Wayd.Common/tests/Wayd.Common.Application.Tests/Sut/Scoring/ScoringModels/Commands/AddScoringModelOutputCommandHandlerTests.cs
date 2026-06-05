using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class AddScoringModelOutputCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly ScoringModelFaker _faker = new();

    private static readonly (string Name, (string Label, decimal Value)[] Levels)[] Scales =
        [("Impact", [("High", 8m), ("Medium", 5m), ("Low", 1m)])];

    private static readonly (string Name, string Token, decimal? Weight, string? ScaleName)[] Criteria =
    [
        ("Business Value", "BV", null, "Impact"),
        ("Job Size", "JS", null, "Impact")
    ];

    private AddScoringModelOutputCommandHandler CreateHandler() =>
        new(_dbContext, NullLogger<AddScoringModelOutputCommandHandler>.Instance);

    // A model with criteria but no outputs yet, so the command under test adds the first (primary) output.
    private ScoringModel SeedModelWithoutOutputs()
    {
        var model = _faker.AsProposedWith(Scales, Criteria, []);
        _dbContext.ScoringModels.Add(model);
        return model;
    }

    [Fact]
    public async Task Handle_ShouldAddOutputAndReturnItsId()
    {
        // Arrange
        var model = SeedModelWithoutOutputs();
        var command = new AddScoringModelOutputCommand(
            model.Id, "WSJF", "WSJF", "BV / JS", IsPrimary: true);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var output = model.Outputs.Single();
        output.Id.Should().Be(result.Value);
        output.IsPrimary.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenFormulaReferencesUnknownToken()
    {
        // Arrange
        var model = SeedModelWithoutOutputs();
        var command = new AddScoringModelOutputCommand(
            model.Id, "Bad", "BAD", "BV + NOPE", IsPrimary: true);

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
        SeedModelWithoutOutputs();
        var command = new AddScoringModelOutputCommand(
            Guid.NewGuid(), "WSJF", "WSJF", "BV / JS", IsPrimary: true);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
