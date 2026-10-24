using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// Runs the time in status report through a real provider.
/// </summary>
/// <remarks>
/// The handler tests run as LINQ-to-Objects, where the correlated "changed within the window" filter
/// always works. These cover that it translates to SQL and returns the stays a hand calculation expects.
/// The history is written under an owner type of its own, so other tests' records sharing the database
/// cannot reach the figures.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class GetTimeInStatusReportQueryIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly WorkflowOwnerDescriptor ReportOwner = new(
        "test.time-in-status-report", "Report Widget", new Dictionary<int, string>(), []);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static Instant March(int day) => Instant.FromUtc(2026, 3, day, 0, 0);

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(March(1));
        provider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 3, 1));

        return provider.Object;
    }

    /// <summary>A seeded workflow, only so the transitions reference real statuses.</summary>
    private static async Task<StatusWorkflow> AnyWorkflow(WaydDbContext context)
    {
        ProductWorkflowOwners.Register();

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem,
                TestContext.Current.CancellationToken);

        if (workflow is null)
        {
            await new ProductManagementWorkflowSeeder()
                .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);

            workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem,
                    TestContext.Current.CancellationToken);
        }

        return workflow;
    }

    private static void AddHistory(WaydDbContext context, Guid recordId, params (StatusRef Status, Instant On)[] steps)
    {
        StatusRef? from = null;

        for (var i = 0; i < steps.Length; i++)
        {
            context.StatusTransitions.Add(new StatusTransition(
                ReportOwner.Key, recordId, from, steps[i].Status, EventActor.System, steps[i].On, i));
            from = steps[i].Status;
        }
    }

    [Fact]
    public async Task Handle_ShouldTranslateAndAverageTheStaysThatEndedInTheWindow()
    {
        // Arrange — stays in the first status of 2 and 6 days end in the window; one ends after it.
        WorkflowOwners.Register(ReportOwner);
        await using var context = _fixture.CreateContext();
        var workflow = await AnyWorkflow(context);
        var statuses = workflow.Statuses.OrderBy(s => s.Order).Select(StatusRef.From).ToList();
        var (first, second) = (statuses[0], statuses[1]);

        var window = (From: Instant.FromUtc(2031, 3, 1, 0, 0), To: Instant.FromUtc(2031, 3, 31, 0, 0));
        Instant Day(int day) => Instant.FromUtc(2031, 3, day, 0, 0);

        AddHistory(context, Guid.CreateVersion7(), (first, Day(1)), (second, Day(3)));
        AddHistory(context, Guid.CreateVersion7(), (first, Day(2)), (second, Day(8)));
        AddHistory(context, Guid.CreateVersion7(), (first, Day(20)), (second, Instant.FromUtc(2031, 4, 9, 0, 0)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = _fixture.CreateContext();
        var sut = new GetTimeInStatusReportQueryHandler(reader);

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery(ReportOwner.Key, window.From, window.To),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var row = result.Value.Statuses.Single(r => r.StatusId == first.StatusId);
        row.Visits.Should().Be(2);
        row.AverageSeconds.Should().Be((long)Duration.FromDays(4).TotalSeconds);
        row.MedianSeconds.Should().Be((long)Duration.FromDays(4).TotalSeconds);
    }
}
