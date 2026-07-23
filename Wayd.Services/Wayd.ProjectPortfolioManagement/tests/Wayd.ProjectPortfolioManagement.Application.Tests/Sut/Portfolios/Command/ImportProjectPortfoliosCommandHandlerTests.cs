using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Portfolios.Command;

public class ImportProjectPortfoliosCommandHandlerTests : IDisposable
{
    private static readonly LocalDate _start = new(2024, 7, 1);
    private static readonly LocalDate _end = new(2026, 6, 30);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext;
    private readonly ImportProjectPortfoliosCommandHandler _handler;
    private readonly Mock<ILogger<ImportProjectPortfoliosCommandHandler>> _mockLogger;

    public ImportProjectPortfoliosCommandHandlerTests()
    {
        _dbContext = new FakeProjectPortfolioManagementDbContext();
        _mockLogger = new Mock<ILogger<ImportProjectPortfoliosCommandHandler>>();

        _handler = new ImportProjectPortfoliosCommandHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ImportsProposedPortfolio_WithoutDates()
    {
        // Arrange
        var command = new ImportProjectPortfoliosCommand([Row("Growth", ProjectPortfolioStatus.Proposed, start: null)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var portfolio = _dbContext.Portfolios.Single();
        portfolio.Name.Should().Be("Growth");
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Proposed);
        portfolio.DateRange.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ActivatesPortfolio_WithTheRowsOwnStartDate()
    {
        // Arrange
        // The row's date must win: the activate command hardcodes today, which would flatten history.
        var command = new ImportProjectPortfoliosCommand([Row("Growth", ProjectPortfolioStatus.Active, _start)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var portfolio = _dbContext.Portfolios.Single();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Active);
        portfolio.DateRange!.Start.Should().Be(_start);
    }

    [Fact]
    public async Task Handle_PausesPortfolio_WhenStatusIsOnHold()
    {
        // Arrange
        var command = new ImportProjectPortfoliosCommand([Row("Growth", ProjectPortfolioStatus.OnHold, _start)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Portfolios.Single().Status.Should().Be(ProjectPortfolioStatus.OnHold);
    }

    [Theory]
    [InlineData(ProjectPortfolioStatus.Closed)]
    [InlineData(ProjectPortfolioStatus.Archived)]
    public async Task Handle_LeavesPortfolioActive_WhenStatusIsTerminal(ProjectPortfolioStatus status)
    {
        // Arrange
        // A portfolio cannot close until its contents are closed, so the finalize import finishes the job.
        var command = new ImportProjectPortfoliosCommand([Row("Growth", status, _start, _end)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var portfolio = _dbContext.Portfolios.Single();
        portfolio.Status.Should().Be(ProjectPortfolioStatus.Active);
        portfolio.DateRange!.Start.Should().Be(_start);
    }

    [Fact]
    public async Task Handle_AssignsRoles_ResolvedByEmployeeNumber()
    {
        // Arrange
        var sponsor = new EmployeeFaker().WithEmployeeNumber("E100").Generate();
        var owner = new EmployeeFaker().WithEmployeeNumber("E200").Generate();
        _dbContext.AddEmployees([sponsor, owner]);

        var row = Row("Growth", ProjectPortfolioStatus.Active, _start) with
        {
            SponsorEmployeeNumbers = ["E100"],
            OwnerEmployeeNumbers = ["E200"],
        };

        // Act
        var result = await _handler.Handle(new ImportProjectPortfoliosCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var roles = _dbContext.Portfolios.Single().Roles;
        roles.Should().ContainSingle(r => r.Role == ProjectPortfolioRole.Sponsor && r.EmployeeId == sponsor.Id);
        roles.Should().ContainSingle(r => r.Role == ProjectPortfolioRole.Owner && r.EmployeeId == owner.Id);
    }

    [Fact]
    public async Task Handle_Fails_WhenAnEmployeeNumberCannotBeResolved()
    {
        // Arrange
        var row = Row("Growth", ProjectPortfolioStatus.Active, _start) with { OwnerEmployeeNumbers = ["MISSING"] };

        // Act
        var result = await _handler.Handle(new ImportProjectPortfoliosCommand([row]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MISSING");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenANameIsDuplicatedWithinTheBatch()
    {
        // Arrange
        var command = new ImportProjectPortfoliosCommand([
            Row("Growth", ProjectPortfolioStatus.Active, _start),
            Row("growth", ProjectPortfolioStatus.Active, _start),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenAPortfolioAlreadyExists()
    {
        // Arrange
        // Names are the natural key other imports resolve against, so a collision has to be rejected
        // rather than silently creating a second portfolio of the same name.
        _dbContext.AddPortfolio(new ProjectPortfolioFaker().WithName("Growth").Generate());

        var command = new ImportProjectPortfoliosCommand([Row("Growth", ProjectPortfolioStatus.Active, _start)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exist");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SavesOnce_ForTheWholeBatch()
    {
        // Arrange
        var command = new ImportProjectPortfoliosCommand([
            Row("Growth", ProjectPortfolioStatus.Active, _start),
            Row("Efficiency", ProjectPortfolioStatus.Proposed, start: null),
            Row("Platform", ProjectPortfolioStatus.OnHold, _start),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Portfolios.Should().HaveCount(3);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    private static ImportProjectPortfolioDto Row(string name, ProjectPortfolioStatus status, LocalDate? start, LocalDate? end = null) =>
        new(name, $"{name} portfolio", status, start, end, [], [], []);

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
