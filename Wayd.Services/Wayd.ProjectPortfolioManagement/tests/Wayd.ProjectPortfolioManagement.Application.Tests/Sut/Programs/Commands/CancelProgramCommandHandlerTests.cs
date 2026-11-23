using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.Programs.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.Commands;

public sealed class CancelProgramCommandHandlerTests : IDisposable
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 20, 9, 0)));
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _actorEmployeeId = Guid.NewGuid();

    public CancelProgramCommandHandlerTests()
    {
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_actorEmployeeId);
        _currentPrincipal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUser.Setup(u => u.GetUserId()).Returns(Guid.NewGuid().ToString());
    }

    private CancelProgramCommandHandler CreateHandler() => new(
        _dbContext, _currentPrincipal.Object, _currentUser.Object, Mock.Of<ILogger<CancelProgramCommandHandler>>(), _dateTimeProvider);

    /// <summary>
    /// A program the actor owns, holding the given projects. Its navigations are set directly because the fake
    /// context ignores Include.
    /// </summary>
    private Program SeedProgram(bool active, params Project[] projects)
    {
        var portfolio = new ProjectPortfolioFaker().AsActive(_dateTimeProvider);
        var faker = new ProgramFaker()
            .WithRoles(new Dictionary<ProgramRole, HashSet<Guid>> { [ProgramRole.Owner] = [_actorEmployeeId] });
        var program = active ? faker.AsActive(_dateTimeProvider, portfolio.Id) : faker.AsProposed(portfolio.Id);
        program.SetPrivate(p => p.Portfolio, portfolio);
        var programProjects = GenericExtensions.GetPrivateHashSet<Project>(program, "_projects");
        foreach (var project in projects)
            programProjects.Add(project);
        _dbContext.AddProgram(program);
        return program;
    }

    [Fact]
    public async Task Handle_ShouldCancelAProposedProgram()
    {
        // Arrange
        var program = SeedProgram(active: false);

        // Act
        var result = await CreateHandler().Handle(new CancelProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        program.Status.Should().Be(ProgramStatus.Canceled);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenAnActiveProgramHasAnOpenProject()
    {
        // Arrange
        var program = SeedProgram(active: true, new ProjectFaker().AsActive(_dateTimeProvider));

        // Act
        var result = await CreateHandler().Handle(new CancelProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All projects must be completed or canceled before the program can be canceled.");
        program.Status.Should().Be(ProgramStatus.Active);
        program.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProgramDoesNotExist()
    {
        // Arrange
        SeedProgram(active: false);

        // Act
        var result = await CreateHandler().Handle(new CancelProgramCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

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
        var program = SeedProgram(active: false);

        // Act
        var act = () => CreateHandler().Handle(new CancelProgramCommand(program.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*isn't linked to an employee record*");
        program.Status.Should().Be(ProgramStatus.Proposed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
