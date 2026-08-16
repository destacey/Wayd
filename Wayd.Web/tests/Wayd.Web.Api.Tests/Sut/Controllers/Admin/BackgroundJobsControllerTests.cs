using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Web.Api.Controllers.Admin;
using Wayd.Web.Api.Interfaces;
using Wayd.Web.Api.Models.Admin;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Admin;

public sealed class BackgroundJobsControllerTests
{
    private readonly AutoMocker _mocker = new();

    private BackgroundJobsController CreateController()
    {
        var controller = _mocker.CreateInstance<BackgroundJobsController>();
        // ProblemDetailsExtensions.ForBadRequest reads request/trace info off HttpContext.
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static string PolicyFor(string methodName)
    {
        var attribute = typeof(BackgroundJobsController)
            .GetMethod(methodName)!
            .GetCustomAttribute<MustHavePermissionAttribute>();

        return attribute?.Policy ?? string.Empty;
    }

    [Theory]
    [InlineData(nameof(BackgroundJobsController.GetJobs), ApplicationAction.View)]
    [InlineData(nameof(BackgroundJobsController.GetJobDetail), ApplicationAction.View)]
    [InlineData(nameof(BackgroundJobsController.GetStatistics), ApplicationAction.View)]
    [InlineData(nameof(BackgroundJobsController.GetServers), ApplicationAction.View)]
    [InlineData(nameof(BackgroundJobsController.GetRecurringJobs), ApplicationAction.View)]
    [InlineData(nameof(BackgroundJobsController.RequeueJob), ApplicationAction.Run)]
    [InlineData(nameof(BackgroundJobsController.DeleteJob), ApplicationAction.Delete)]
    [InlineData(nameof(BackgroundJobsController.RemoveRecurringJob), ApplicationAction.Delete)]
    public void Endpoints_RequireTheExpectedBackgroundJobsPermission(string methodName, string expectedAction)
    {
        // Arrange
        var expected = ApplicationPermission.NameFor(expectedAction, ApplicationResource.BackgroundJobs);

        // Act
        var policy = PolicyFor(methodName);

        // Assert — these endpoints expose job control, so an unguarded one is a privilege hole.
        policy.Should().Be(expected);
    }

    [Fact]
    public void Create_ForNonSchedulableJobType_ReturnsBadRequest()
    {
        // Arrange — the UI filters its picker on IsSchedulable, so this models a direct API call.
        var controller = CreateController();
        var request = new CreateRecurringJobRequest
        {
            JobId = "iterations-sync",
            JobTypeId = (int)BackgroundJobType.IterationsSync,
            CronExpression = "*/5 * * * *",
        };

        // Act
        var result = controller.Create(request, _mocker.Get<IJobManager>(), TestContext.Current.CancellationToken);

        // Assert — a 400, not the ArgumentOutOfRangeException (500) this used to throw.
        result.Should().BeOfType<BadRequestObjectResult>();
        _mocker.GetMock<IJobService>().Verify(
            s => s.AddOrUpdate(It.IsAny<string>(), It.IsAny<Expression<Func<Task>>>(), It.IsAny<Func<string>>()),
            Times.Never);
    }

    [Fact]
    public void Create_ForSchedulableJobType_SchedulesTheJob()
    {
        // Arrange
        var controller = CreateController();
        var request = new CreateRecurringJobRequest
        {
            JobId = "people-full-sync",
            JobTypeId = (int)BackgroundJobType.PeopleFullSync,
            CronExpression = "0 2 * * *",
        };

        // Act
        var result = controller.Create(request, _mocker.Get<IJobManager>(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        _mocker.GetMock<IJobService>().Verify(
            s => s.AddOrUpdate(request.JobId, It.IsAny<Expression<Func<Task>>>(), It.IsAny<Func<string>>()),
            Times.Once);
    }

    [Fact]
    public void Create_EverySchedulableJobType_MapsToARecurringInvocation()
    {
        // Arrange — the guard set and the expression switch are separate code; this is the test that
        // catches them drifting apart (a type marked schedulable with no mapped invocation throws).
        var schedulable = Enum.GetValues<BackgroundJobType>().Where(SchedulableBackgroundJobTypes.Contains);

        foreach (var jobType in schedulable)
        {
            var controller = CreateController();
            var request = new CreateRecurringJobRequest
            {
                JobId = $"job-{(int)jobType}",
                JobTypeId = (int)jobType,
                CronExpression = "0 3 * * *",
            };

            // Act
            var act = () => controller.Create(request, _mocker.Get<IJobManager>(), TestContext.Current.CancellationToken);

            // Assert
            act.Should().NotThrow($"{jobType} is marked schedulable so it must have a recurring invocation mapped");
        }
    }

    [Fact]
    public void RemoveRecurringJob_WhenJobDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _mocker.GetMock<IJobService>().Setup(s => s.RemoveRecurringJob("missing")).Returns(false);

        // Act
        var result = CreateController().RemoveRecurringJob("missing");

        // Assert — a removal that matched nothing must not read as success.
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void RemoveRecurringJob_WhenRemoved_ReturnsNoContent()
    {
        // Arrange
        _mocker.GetMock<IJobService>().Setup(s => s.RemoveRecurringJob("nightly")).Returns(true);

        // Act
        var result = CreateController().RemoveRecurringJob("nightly");

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void GetJobDetail_WhenJobIsUnknown_ReturnsNotFound()
    {
        // Arrange
        _mocker.GetMock<IJobService>().Setup(s => s.GetJobDetail("nope")).Returns((JobDetailDto?)null);

        // Act
        var result = CreateController().GetJobDetail("nope");

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    public void GetJobs_ClampsNegativePageNumbersToZero(int requested, int expected)
    {
        // Arrange
        _mocker.GetMock<IJobService>()
            .Setup(s => s.GetJobs(It.IsAny<JobStateFilter>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new JobPageDto());

        // Act
        CreateController().GetJobs(JobStateFilter.Failed, requested, 50);

        // Assert
        _mocker.GetMock<IJobService>().Verify(s => s.GetJobs(JobStateFilter.Failed, expected, 50), Times.Once);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(9000, 500)]
    public void GetJobs_ClampsPageSizeToTheSupportedRange(int requested, int expected)
    {
        // Arrange
        _mocker.GetMock<IJobService>()
            .Setup(s => s.GetJobs(It.IsAny<JobStateFilter>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new JobPageDto());

        // Act
        CreateController().GetJobs(JobStateFilter.Processing, 0, requested);

        // Assert — an unbounded page size lets one request pull the whole job store.
        _mocker.GetMock<IJobService>().Verify(s => s.GetJobs(JobStateFilter.Processing, 0, expected), Times.Once);
    }
}
