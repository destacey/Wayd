using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Web.Api.Controllers;
using Wayd.Web.Api.Mcp;
using Wayd.Web.Api.Models.Me;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Tests.Sut.Controllers;

public sealed class MeControllerTests
{
    private const string UserId = "a1b2c3d4-0000-0000-0000-000000000001";

    private readonly AutoMocker _mocker = new();

    private MeController CreateController()
    {
        var controller = _mocker.CreateInstance<MeController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private void LinkedTo(Guid? employeeId) =>
        _mocker
            .GetMock<ICurrentPrincipal>()
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);

    private void VerifyNothingDispatched() =>
        _mocker.GetMock<IDispatcher>().VerifyNoOtherCalls();

    [Fact]
    public async Task GetProfile_ReturnsTheAccountAndItsLinkedEmployee()
    {
        // Arrange
        var employee = new NavigationDto { Id = Guid.NewGuid(), Key = 42, Name = "Avery Quinn" };
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns(UserId);
        _mocker
            .GetMock<IUserService>()
            .Setup(s => s.GetAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDetailsDto
            {
                Id = UserId,
                UserName = "avery.quinn@acme.example",
                FirstName = "Avery",
                LastName = "Quinn",
                Email = "avery.quinn@acme.example",
                LoginProvider = "Wayd",
                Employee = employee,
                Roles = [new RoleListDto { Id = "r2", Name = "Team Member" }, new RoleListDto { Id = "r1", Name = "Basic" }],
            });

        var controller = CreateController();

        // Act
        var result = await controller.GetProfile(TestContext.Current.CancellationToken);

        // Assert
        var me = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MeResponse>().Subject;
        me.UserId.Should().Be(UserId);
        me.Employee.Should().Be(employee);
        me.Roles.Should().Equal("Basic", "Team Member");
    }

    [Fact]
    public async Task GetProfile_WhenTheAccountHasNoEmployee_ReturnsANullEmployee()
    {
        // Arrange
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns(UserId);
        _mocker
            .GetMock<IUserService>()
            .Setup(s => s.GetAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDetailsDto { Id = UserId, LoginProvider = "Wayd" });

        var controller = CreateController();

        // Act
        var result = await controller.GetProfile(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MeResponse>()
            .Which.Employee.Should().BeNull();
    }

    [Fact]
    public async Task GetTeamMemberships_AsksForTheCallersEmployee()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        LinkedTo(employeeId);
        IReadOnlyList<TeamMemberDto> memberships = [];
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeTeamMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships);

        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(memberships);
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.Is<GetEmployeeTeamMembershipsQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamMemberships_WhenNotLinkedToAnEmployee_ReturnsNotFoundSayingWhy()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Contain("not linked to an employee");
        VerifyNothingDispatched();
    }

    [Fact]
    public async Task GetAllocations_AsksForTheCallersEmployee()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        LinkedTo(employeeId);
        IReadOnlyList<EmployeeAllocationDto> allocations = [];
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeAllocationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(allocations);

        var controller = CreateController();

        // Act
        var result = await controller.GetAllocations(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(allocations);
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.Is<GetEmployeeAllocationsQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllocations_WhenNotLinkedToAnEmployee_ReturnsNotFound()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetAllocations(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        VerifyNothingDispatched();
    }

    [Fact]
    public async Task GetWorkItems_AsksForTheCallersEmployeeWithTheFilters()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        LinkedTo(employeeId);
        WorkStatusCategory[] open = [WorkStatusCategory.Proposed, WorkStatusCategory.Active];
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeWorkItemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemListDto>());

        var controller = CreateController();

        // Act
        var result = await controller.GetWorkItems(open, null, null, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(
                It.Is<GetEmployeeWorkItemsQuery>(q => q.EmployeeId == employeeId && q.StatusCategories == open && q.DoneFrom == null && q.DoneTo == null),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWorkItems_WhenNotLinkedToAnEmployee_ReturnsNotFound()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetWorkItems(null, null, null, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        VerifyNothingDispatched();
    }

    [Theory]
    [InlineData(nameof(MeController.GetProfile))]
    [InlineData(nameof(MeController.GetTeamMemberships))]
    [InlineData(nameof(MeController.GetAllocations))]
    public void ReadingYourOwnRecords_NeedsNoPermission(string action)
    {
        // Arrange
        var method = typeof(MeController).GetMethod(action)!;

        // Act
        var permission = method.GetCustomAttribute<MustHavePermissionAttribute>();

        // Assert
        permission.Should().BeNull();
    }

    [Fact]
    public void Tools_AreOfferedOnEveryConnection()
    {
        // Arrange
        var controller = typeof(MeController);

        // Act
        var placement = controller.GetCustomAttribute<McpToolsAttribute>()!;

        // Assert
        placement.Toolset.Should().BeNull();
        placement.Skill.Should().Be("wayd-users");
    }
}
