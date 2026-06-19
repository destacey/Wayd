using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Scoring.Enums;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class CreateScoringModelCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();

    private CreateScoringModelCommandHandler CreateHandler() =>
        new(_dbContext, NullLogger<CreateScoringModelCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ShouldPersistModelAndReturnItsId()
    {
        // Arrange — a complete WSJF definition supplied as command input.
        var command = new CreateScoringModelCommand(
            "WSJF",
            "Weighted shortest job first.",
            Scales: [new("Impact", [new("High", 8m), new("Medium", 5m), new("Low", 1m)])],
            Criteria:
            [
                new("Business Value", "BV", null, null, "Impact"),
                new("Time Criticality", "TC", null, null, "Impact"),
                new("Risk Reduction", "RR", null, null, "Impact"),
                new("Job Size", "JS", null, null, "Impact"),
            ],
            Outputs:
            [
                new("Cost of Delay", "CoD", "BV + TC + RR", false),
                new("WSJF", "WSJF", "CoD / JS", true),
            ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        var persisted = await _dbContext.ScoringModels.SingleAsync(TestContext.Current.CancellationToken);
        persisted.Id.Should().Be(result.Value);
        persisted.Name.Should().Be("WSJF");
        persisted.State.Should().Be(ScoringModelState.Proposed);
        persisted.Criteria.Should().HaveCount(4);
        persisted.Outputs.Should().HaveCount(2);
        persisted.Outputs.Single(o => o.IsPrimary).Token.Should().Be("WSJF");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }
}
