using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Commands;

public sealed class ActivateProgramCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    public ActivateProgramCommandHandlerTests()
    {
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
    }

    private ActivateProgramCommandHandler CreateHandler() => new(
        _dbContext, _currentPrincipal.Object, _currentUser.Object, Mock.Of<ILogger<ActivateProgramCommandHandler>>(), _dateTimeProvider);

    /// <summary>
    /// A program the actor owns, in a portfolio. The Portfolio navigation is set directly because the fake
    /// context ignores Include.
    /// </summary>
    private Program SeedProgram(ProgramStatus status, bool actorIsOwner = true)
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var start = new LocalDate(2026, 11, 1);
        var program = new ProgramFaker()
            .WithStatus(status)
            .WithDateRange(new LocalDateRange(start, start.PlusMonths(3)))
            .WithPortfolioId(portfolio.Id)
            .WithRoles(actorIsOwner ? new Dictionary<ProgramRole, HashSet<Guid>> { [ProgramRole.Owner] = [_actorEmployeeId] } : null)
            .Generate();
        program.SetPrivate(p => p.Portfolio, portfolio);
        _dbContext.AddProgram(program);
        return program;
    }

    [Fact]
    public async Task Handle_ShouldActivateAProposedProgram()
    {
        // Arrange
        var program = SeedProgram(ProgramStatus.Proposed);

        // Act
        var result = await CreateHandler().Handle(new ActivateProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        program.Status.Should().Be(ProgramStatus.Active);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheProgramIsNotProposed()
    {
        // Arrange
        var program = SeedProgram(ProgramStatus.Active);

        // Act
        var result = await CreateHandler().Handle(new ActivateProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed programs can be activated.");
        program.Status.Should().Be(ProgramStatus.Active);
        program.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheActorDoesNotLeadTheProgram()
    {
        // Arrange
        var program = SeedProgram(ProgramStatus.Proposed, actorIsOwner: false);

        // Act
        var result = await CreateHandler().Handle(new ActivateProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("You are not authorized to manage this program. Program or portfolio Owners and Managers may.");
        program.Status.Should().Be(ProgramStatus.Proposed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProgramDoesNotExist()
    {
        // Arrange
        SeedProgram(ProgramStatus.Proposed);

        // Act
        var result = await CreateHandler().Handle(new ActivateProgramCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Program not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WhenUserHasNoLinkedEmployee()
    {
        // Arrange — every managed PPM action must be attributable to an employee.
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var program = SeedProgram(ProgramStatus.Proposed);

        // Act
        var act = () => CreateHandler().Handle(new ActivateProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        program.Status.Should().Be(ProgramStatus.Proposed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
