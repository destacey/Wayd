using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Web.Api.Controllers.Organizations;
using Wayd.Web.Api.Models.Organizations.Employees;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Organizations;

public sealed class EmployeesControllerTests
{
    private readonly AutoMocker _mocker = new();

    private EmployeesController CreateController()
    {
        var controller = _mocker.CreateInstance<EmployeesController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private void EmployeeExists(bool exists) =>
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<EmployeeExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);

    private void VerifyNeverSent<TCommand>() where TCommand : ICommand =>
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.IsAny<TCommand>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Delete_SendsTheEmployeeIdAndTheRequestsCancellationToken()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        EmployeeExists(true);
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<DeleteEmployeeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var controller = CreateController();

        // Act
        var result = await controller.Delete(employeeId, cancellation.Token);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.Is<DeleteEmployeeCommand>(c => c.Id == employeeId), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenTheCommandFails_ReturnsBadRequestWithTheReason()
    {
        // Arrange
        EmployeeExists(true);
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<DeleteEmployeeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Error handling DeleteEmployeeCommand command."));

        var controller = CreateController();

        // Act
        var result = await controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("Error handling DeleteEmployeeCommand command.");
    }

    [Fact]
    public async Task Delete_UnknownEmployee_ReturnsNotFoundWithoutSendingTheCommand()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        EmployeeExists(false);

        var controller = CreateController();

        // Act
        var result = await controller.Delete(employeeId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.Is<EmployeeExistsQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()), Times.Once);
        VerifyNeverSent<DeleteEmployeeCommand>();
    }

    [Fact]
    public async Task GetTeamMemberships_UnknownEmployee_ReturnsNotFound()
    {
        // Arrange
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeTeamMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TeamMemberDto>?)null);

        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetTeamMemberships_EmployeeOnNoTeam_ReturnsAnEmptyList()
    {
        // Arrange
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeTeamMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamMemberDto>());

        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<TeamMemberDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangeHolidayCalendar_UnknownEmployee_ReturnsNotFoundWithoutSendingTheCommand()
    {
        // Arrange
        EmployeeExists(false);
        var controller = CreateController();

        // Act
        var result = await controller.ChangeHolidayCalendar(Guid.NewGuid(), new ChangeEmployeeHolidayCalendarRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        VerifyNeverSent<ChangeEmployeeHolidayCalendarCommand>();
    }

    [Fact]
    public async Task ChangeHolidayCalendar_KnownEmployee_WhenTheCommandFails_ReturnsBadRequest()
    {
        // Arrange
        EmployeeExists(true);
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<ChangeEmployeeHolidayCalendarCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Holiday calendar not found."));
        var controller = CreateController();

        // Act
        var result = await controller.ChangeHolidayCalendar(Guid.NewGuid(), new ChangeEmployeeHolidayCalendarRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangeWorkingDayHours_UnknownEmployee_ReturnsNotFoundWithoutSendingTheCommand()
    {
        // Arrange
        EmployeeExists(false);
        var controller = CreateController();

        // Act
        var result = await controller.ChangeWorkingDayHours(Guid.NewGuid(), new ChangeEmployeeWorkingDayHoursRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        VerifyNeverSent<ChangeEmployeeWorkingDayHoursCommand>();
    }

    [Fact]
    public async Task ChangeAllocations_UnknownEmployee_ReturnsNotFoundWithoutSendingTheCommand()
    {
        // Arrange
        EmployeeExists(false);
        var controller = CreateController();

        // Act
        var result = await controller.ChangeAllocations(Guid.NewGuid(), new ChangeEmployeeAllocationsRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        VerifyNeverSent<ChangeEmployeeAllocationsCommand>();
    }

    [Fact]
    public async Task MoveAllocation_UnknownEmployee_ReturnsNotFoundWithoutSendingTheCommand()
    {
        // Arrange
        EmployeeExists(false);
        var controller = CreateController();

        // Act
        var result = await controller.MoveAllocation(Guid.NewGuid(), new MoveEmployeeAllocationRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        VerifyNeverSent<MoveEmployeeAllocationCommand>();
    }

    [Fact]
    public async Task RecordTimeAway_MissingEmployee_ReturnsNotFoundWithoutSendingTheCommand()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.Is<IsTimeAwayEmployeeMissingQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = CreateController();
        var request = new EmployeeTimeAwayRequest { Start = new LocalDate(2027, 2, 1), End = new LocalDate(2027, 2, 1), PercentAway = 100, Reason = TimeAwayReason.Training };

        // Act
        var result = await controller.RecordTimeAway(employeeId, request, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.IsAny<RecordEmployeeTimeAwayCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeTimeAway_MissingEmployee_ReturnsNotFoundWithoutSendingTheCommand()
    {
        // Arrange
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<IsTimeAwayEmployeeMissingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = CreateController();

        // Act
        var result = await controller.ChangeTimeAway(Guid.NewGuid(), Guid.NewGuid(), new EmployeeTimeAwayRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        VerifyNeverSent<ChangeEmployeeTimeAwayCommand>();
    }

    [Theory]
    [InlineData(true, typeof(NotFoundResult))]
    [InlineData(false, typeof(BadRequestObjectResult))]
    public async Task RemoveTimeAway_ReturnsNotFoundOnlyWhenTheCallerMayBeToldTheEmployeeIsMissing(bool missing, Type expected)
    {
        // Arrange
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<IsTimeAwayEmployeeMissingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(missing);
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<RemoveEmployeeTimeAwayCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Only the employee, the people they report to, or a time away administrator can record, change or remove their time away."));
        var controller = CreateController();

        // Act
        var result = await controller.RemoveTimeAway(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType(expected);
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.IsAny<RemoveEmployeeTimeAwayCommand>(), It.IsAny<CancellationToken>()), missing ? Times.Never() : Times.Once());
    }
}
