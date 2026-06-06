using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Scoring.Commands;

public class RecordProjectScoreCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly RecordProjectScoreCommandHandler _handler;
    private readonly Mock<ILogger<RecordProjectScoreCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 5, 1, 0, 0);
    private readonly ProjectFaker _projectFaker = new();
    private readonly ProjectPortfolioFaker _portfolioFaker = new();
    private readonly ScoringModelFaker _scoringModelFaker = new();

    // A free-numeric model (Score = BV / JS) so the command can supply plain numeric values.
    private ScoringModel FreeNumericModel() =>
        _scoringModelFaker.AsActiveWith(
            scales: [],
            criteria:
            [
                ("Business Value", "BV", null, null),
                ("Job Size", "JS", null, null),
            ],
            outputs:
            [
                ("Score", "Score", "BV / JS", true),
            ]);

    public RecordProjectScoreCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns(_currentEmployeeId);
        _mockDateTimeProvider.Setup(d => d.Now).Returns(_now);

        _handler = new RecordProjectScoreCommandHandler(
            _dbContext, _mockDateTimeProvider.Object, _mockCurrentUser.Object, _mockLogger.Object);
    }

    private (Project Project, ScoringModel Model) ScorableProject(bool withModel = true, bool ownerIsActor = true)
    {
        var model = FreeNumericModel();
        var portfolioId = Guid.NewGuid();
        var portfolio = _portfolioFaker.WithData(id: portfolioId).Generate();
        if (withModel)
        {
            portfolio.SetPrivate(p => p.ScoringModelId, (Guid?)model.Id);
        }

        var roles = ownerIsActor
            ? new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Owner] = [_currentEmployeeId] }
            : null;
        var project = _projectFaker.WithData(portfolioId: portfolioId, roles: roles).Generate();
        project.SetPrivate(p => p.Portfolio, portfolio);

        _dbContext.AddProject(project);
        _dbContext.AddScoringModel(model);

        return (project, model);
    }

    private RecordProjectScoreCommand CommandFor(ScoringModel model, Guid projectId, decimal bv, decimal js)
    {
        var bvCriterion = model.Criteria.Single(c => c.Token == "BV");
        var jsCriterion = model.Criteria.Single(c => c.Token == "JS");
        return new RecordProjectScoreCommand(projectId,
        [
            new RecordProjectScoreCommand.CriterionRatingInput(bvCriterion.Id, bv, null),
            new RecordProjectScoreCommand.CriterionRatingInput(jsCriterion.Id, js, null),
        ]);
    }

    [Fact]
    public async Task Handle_WhenValid_RecordsScoreAndSaves()
    {
        // Arrange
        var (project, model) = ScorableProject();
        var command = CommandFor(model, project.Id, 10m, 2m);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Scores.Should().ContainSingle();
        project.Scores.Single().Id.Should().Be(result.Value);
        project.Scores.Single().PrimaryValue.Should().Be(5m);
        project.CurrentScore!.Value.Should().Be(5m);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var (_, model) = ScorableProject();
        var command = CommandFor(model, Guid.NewGuid(), 10m, 2m);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNoModelAssigned_ReturnsFailure()
    {
        // Arrange
        var (project, model) = ScorableProject(withModel: false);
        var command = CommandFor(model, project.Id, 10m, 2m);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Scoring is not enabled");
        project.Scores.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenActorNotAuthorized_ReturnsFailure()
    {
        // Arrange
        var (project, model) = ScorableProject(ownerIsActor: false);
        var command = CommandFor(model, project.Id, 10m, 2m);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.Scores.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserHasNoEmployeeId_ReturnsFailure()
    {
        // Arrange
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns((Guid?)null);
        var (project, model) = ScorableProject();
        var command = CommandFor(model, project.Id, 10m, 2m);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("employee Id");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCriterionMissingFromRatings_ReturnsFailure()
    {
        // Arrange
        var (project, model) = ScorableProject();
        var bvCriterion = model.Criteria.Single(c => c.Token == "BV");
        var command = new RecordProjectScoreCommand(project.Id,
        [
            new RecordProjectScoreCommand.CriterionRatingInput(bvCriterion.Id, 10m, null),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has not been rated");
        project.Scores.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
