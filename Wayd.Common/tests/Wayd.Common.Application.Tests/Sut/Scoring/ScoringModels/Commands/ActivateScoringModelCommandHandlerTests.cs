using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Scoring.ScoringModels.Commands;

public class ActivateScoringModelCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 1, 15, 9, 30)));
    private readonly ScoringModelFaker _faker = new();

    private ActivateScoringModelCommandHandler CreateHandler() =>
        new(_dbContext, _currentUser.Object, _dateTimeProvider, NullLogger<ActivateScoringModelCommandHandler>.Instance);

    private ScoringModel SeedProposedModel()
    {
        var model = _faker.AsProposedWsjf();
        _dbContext.ScoringModels.Add(model);
        return model;
    }

    [Fact]
    public async Task Handle_ShouldTransitionProposedModelToActive()
    {
        // Arrange
        var model = SeedProposedModel();
        var command = new ActivateScoringModelCommand(model.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        model.State.Should().Be(ScoringModelState.Active);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheModelIsAlreadyActive()
    {
        // Arrange
        var model = SeedProposedModel();
        model.Activate(EventActor.System, _dateTimeProvider.Now).IsSuccess.Should().BeTrue();
        var eventsBefore = model.DomainEvents.Count;
        var command = new ActivateScoringModelCommand(model.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed scoring models can be activated.");
        model.State.Should().Be(ScoringModelState.Active);
        model.DomainEvents.Should().HaveCount(eventsBefore);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenModelNotFound()
    {
        // Arrange
        SeedProposedModel();
        var command = new ActivateScoringModelCommand(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
