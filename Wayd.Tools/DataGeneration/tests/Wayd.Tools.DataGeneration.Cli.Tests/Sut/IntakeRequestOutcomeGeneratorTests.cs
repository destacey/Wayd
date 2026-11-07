using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The generated outcomes, checked against the rules the outcome import enforces and the story the data has to
/// tell: fulfilled requests met by accepted deliverables, accepted ones still waiting on theirs.
/// </summary>
public class IntakeRequestOutcomeGeneratorTests
{
    private static readonly DateOnly _asOf = new(2026, 6, 15);

    private static (GeneratedPpm Ppm, GeneratedDemandManagement Data) Generate(int seed = 1234)
    {
        var context = new GenerationContext { AsOf = _asOf, Seed = seed };
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 18 }, context).Generate();
        var ppm = new PpmGenerator(org.Structure, new PpmOptions(), context).Generate();
        var demandManagement = new DemandManagementGenerator(org, new DemandManagementOptions(), context).Generate();

        return (ppm, demandManagement with { Outcomes = new IntakeRequestOutcomeGenerator(demandManagement, ppm, context).Generate() });
    }

    private static readonly (GeneratedPpm Ppm, GeneratedDemandManagement Data) _default = Generate();

    private static IReadOnlyList<IntakeRequestOutcomeModel> Outcomes => _default.Data.Outcomes;

    private static IntakeRequestModel RequestOf(IntakeRequestOutcomeModel outcome) =>
        _default.Data.Requests.Single(r => r.Handle == outcome.RequestHandle);

    private static IEnumerable<ProjectDeliverableModel> DeliverablesOf(IntakeRequestOutcomeModel outcome) =>
        outcome.DeliverableNames.Select(n => _default.Ppm.ProjectDeliverables.Single(d => d.ProjectKey == outcome.ProjectKey && d.Name == n));

    [Fact]
    public void Generate_ProducesBothKindsForAcceptedAndFulfilledRequestsOnly()
    {
        // Arrange & Act
        var outcomes = Outcomes;

        // Assert
        outcomes.Select(o => o.Kind).Distinct().Should().BeEquivalentTo(
            [IntakeRequestOutcomeGenerator.NewProject, IntakeRequestOutcomeGenerator.ExistingProjectScope]);
        outcomes.Select(o => RequestOf(o).Status).Should().OnlyContain(s => s == "Accepted" || s == "Fulfilled");
        outcomes.Select(o => (o.RequestHandle, o.ProjectKey)).Should().OnlyHaveUniqueItems(
            "the import refuses a second outcome pointing at the same project");
    }

    [Fact]
    public void Generate_MapsExistingProjectScopeToOneToThreeOfItsProjectsDeliverables_WithANote()
    {
        // Arrange & Act
        var scope = Outcomes.Where(o => o.Kind == IntakeRequestOutcomeGenerator.ExistingProjectScope).ToList();

        // Assert
        scope.Should().NotBeEmpty();
        scope.Should().AllSatisfy(o =>
        {
            o.DeliverableNames.Should().HaveCountGreaterThanOrEqualTo(1).And.HaveCountLessThanOrEqualTo(3).And.OnlyHaveUniqueItems();
            DeliverablesOf(o).Should().HaveCount(o.DeliverableNames.Count, "every name is a deliverable of that project");
            o.Note.Should().NotBeNullOrWhiteSpace().And.Match(n => n.Length <= 1024);
        });
        Outcomes.Where(o => o.Kind == IntakeRequestOutcomeGenerator.NewProject)
            .Should().AllSatisfy(o => o.DeliverableNames.Should().BeEmpty());
    }

    [Fact]
    public void Generate_MapsAFulfilledRequestOnlyToAcceptedDeliverables_AndLeavesSomeAcceptedRequestsWaiting()
    {
        // Arrange
        var scope = Outcomes.Where(o => o.Kind == IntakeRequestOutcomeGenerator.ExistingProjectScope).ToList();

        // Act
        var fulfilled = scope.Where(o => RequestOf(o).Status == "Fulfilled").ToList();
        var waiting = scope.Where(o => RequestOf(o).Status == "Accepted" && DeliverablesOf(o).Any(d => d.Status != "Accepted")).ToList();

        // Assert
        fulfilled.Should().NotBeEmpty();
        fulfilled.SelectMany(DeliverablesOf).Should().OnlyContain(d => d.Status == "Accepted");
        waiting.Should().NotBeEmpty("the data should show requests still waiting on their deliverables");
    }

    [Fact]
    public void Generate_StartsEachNewProjectForOneRequest_NoEarlierThanTheRequest()
    {
        // Arrange & Act
        var started = Outcomes.Where(o => o.Kind == IntakeRequestOutcomeGenerator.NewProject).ToList();

        // Assert
        started.Should().NotBeEmpty();
        started.Select(o => o.ProjectKey).Should().OnlyHaveUniqueItems();
        started.Should().AllSatisfy(o =>
        {
            var project = _default.Ppm.Projects.Single(p => p.Key == o.ProjectKey);
            project.CreatedOn.Should().BeOnOrAfter(DateOnly.FromDateTime(RequestOf(o).SubmittedAt.UtcDateTime));
            (project.Status == "Completed").Should().Be(RequestOf(o).Status == "Fulfilled");
        });
    }

    [Fact]
    public void Generate_RecordsEachOutcomeAfterItsRequestWasSubmittedAndNotInTheFuture_BySomeoneOnItsDesk()
    {
        // Arrange
        var endOfToday = new DateTimeOffset(_asOf.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Act & Assert
        Outcomes.Should().AllSatisfy(o =>
        {
            var request = RequestOf(o);
            o.AddedOn.Should().BeAfter(request.SubmittedAt).And.BeBefore(endOfToday);
            var desk = _default.Data.Desks.Single(d => d.Name == request.DeskName);
            (o.AddedByEmployeeNumber == request.AssigneeEmployeeNumber || desk.Members.Any(m => m.EmployeeNumber == o.AddedByEmployeeNumber))
                .Should().BeTrue();
        });
    }

    [Fact]
    public void Generate_IsReproducibleAndLeavesTheRequestsAsTheyWere()
    {
        // Arrange
        var context = new GenerationContext { AsOf = _asOf, Seed = 1234 };
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 18 }, context).Generate();
        var requestsAlone = new DemandManagementGenerator(org, new DemandManagementOptions(), context).Generate().Requests;

        // Act
        var again = Generate();

        // Assert
        again.Data.Outcomes.Should().BeEquivalentTo(Outcomes, o => o.WithStrictOrdering());
        again.Data.Requests.Select(r => (r.Handle, r.Status, r.AssigneeEmployeeNumber))
            .Should().Equal(requestsAlone.Select(r => (r.Handle, r.Status, r.AssigneeEmployeeNumber)));
    }
}
