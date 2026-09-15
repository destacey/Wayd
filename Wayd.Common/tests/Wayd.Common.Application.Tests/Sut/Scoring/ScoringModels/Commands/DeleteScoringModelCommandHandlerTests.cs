using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class DeleteScoringModelCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 1, 15, 9, 30)));
    private readonly ScoringModelFaker _faker = new();

    private DeleteScoringModelCommandHandler CreateHandler() =>
        new(_dbContext, _currentUser.Object, _dateTimeProvider, NullLogger<DeleteScoringModelCommandHandler>.Instance);

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
