using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The generated Request Desk, checked against the rules the desk endpoint and the intake request import
/// enforce. A row breaking one of them is rejected at seed time, far from the generator code that produced it.
/// </summary>
public class DemandManagementGeneratorTests
{
    private static readonly DateOnly _asOf = new(2026, 6, 15);

    private static readonly string[] _statuses =
        ["Submitted", "In Review", "Waiting on Requester", "Accepted", "Fulfilled", "Declined", "Withdrawn"];

    private static GenerationContext Context(int seed = 1234) => new() { AsOf = _asOf, Seed = seed };

    private static (GeneratedOrg Org, GeneratedDemandManagement Data) Generate(
        DemandManagementOptions? options = null, OrgOptions? orgOptions = null, int seed = 1234)
    {
        var context = Context(seed);
        var org = new OrgGenerator(orgOptions ?? new OrgOptions { ValueStreams = 3, Teams = 18 }, context).Generate();

        return (org, new DemandManagementGenerator(org, options ?? new DemandManagementOptions(), context).Generate());
    }

    private static readonly (GeneratedOrg Org, GeneratedDemandManagement Data) _default = Generate();

    private static GeneratedDemandManagement Data => _default.Data;

    private static DateTimeOffset StartOfToday => new(_asOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    [Fact]
    public void Generate_ProducesDesksAxesAndRequests()
    {
        // Arrange & Act
        var data = Data;

        // Assert
        data.Desks.Should().NotBeEmpty();
        data.TagCategories.Should().NotBeEmpty();
        data.Requests.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_GivesEachValueStreamAProductDesk()
    {
        // Arrange
        var domains = _default.Org.Structure.ValueStreams.Select(v => v.Domain);

        // Act
        var names = Data.Desks.Select(d => d.Name);

        // Assert
        names.Should().Contain(domains.Select(d => $"{d} Product Desk"));
    }

    [Fact]
    public void Generate_NamesEveryDeskUniquelyWithinTheEndpointsLimits()
    {
        // Arrange & Act
        var desks = Data.Desks;

        // Assert
        desks.Select(d => d.Name).Should().OnlyHaveUniqueItems(n => n.ToUpperInvariant());
        desks.Should().OnlyContain(d => d.Name.Length <= 128 && d.Description.Length <= 1024);
        desks.Should().OnlyContain(d => d.Visibility == "Open" || d.Visibility == "SummaryOnly" || d.Visibility == "Private");
    }

    [Fact]
    public void Generate_StaffsEveryDeskWithAnAdminAndActiveEmployeesHoldingOneRoleEach()
    {
        // Arrange
        var active = _default.Org.Employees.Where(e => e.IsActive).Select(e => e.EmployeeNumber).ToHashSet();

        // Act & Assert — a desk names active employees only, and an employee holds one role on a desk
        foreach (var desk in Data.Desks)
        {
            desk.Members.Should().Contain(m => m.Role == "Admin");
            desk.Members.Should().OnlyContain(m => active.Contains(m.EmployeeNumber));
            desk.Members.Select(m => m.EmployeeNumber).Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void Generate_PutsEveryRequestOnADeskAndAssignsItOnlyToThatDesksMembers()
    {
        // Arrange
        var desks = Data.Desks.ToDictionary(d => d.Name);

        // Act & Assert — the import refuses an assignee who is not an active member of the request's desk
        foreach (var request in Data.Requests)
        {
            desks.Should().ContainKey(request.DeskName);
            if (request.AssigneeEmployeeNumber is { } assignee)
                desks[request.DeskName].Members.Should().Contain(m => m.EmployeeNumber == assignee);
        }
    }

    [Fact]
    public void Generate_RaisesEveryRequestFromSomeoneEmployedOnTheDay()
    {
        // Arrange
        var employees = _default.Org.Employees.ToDictionary(e => e.EmployeeNumber);

        // Act
        var hiredLater = Data.Requests
            .Where(r => DateOnly.FromDateTime(employees[r.RequesterEmployeeNumber].HireDate!.Value) > DateOnly.FromDateTime(r.SubmittedAt.UtcDateTime))
            .ToList();

        // Assert
        hiredLater.Should().BeEmpty();
    }

    [Fact]
    public void Generate_PlacesEveryRequestInThePastWithItsStatusChangeAfterItsSubmission()
    {
        // Arrange & Act
        var requests = Data.Requests;

        // Assert — the import refuses a submission or a status change in the future, or one out of order
        requests.Should().OnlyContain(r => r.SubmittedAt < StartOfToday);
        requests.Where(r => r.StatusChangedAt is not null)
            .Should().OnlyContain(r => r.StatusChangedAt >= r.SubmittedAt && r.StatusChangedAt < StartOfToday);
        requests.Where(r => r.Status == "Submitted").Should().OnlyContain(r => r.StatusChangedAt == null);
        requests.Where(r => r.Status != "Submitted").Should().OnlyContain(r => r.StatusChangedAt != null);
    }

    [Fact]
    public void Generate_UsesOnlyTheDefaultWorkflowsStatusesWithAReasonWhereOneIsNeeded()
    {
        // Arrange & Act
        var requests = Data.Requests;

        // Assert — declining, and finishing with no outcome, need a reason the requester can read
        requests.Should().OnlyContain(r => _statuses.Contains(r.Status));
        requests.Where(r => r.Status is "Declined" or "Fulfilled").Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.StatusReason));
        requests.Select(r => r.Status).Distinct().Should().HaveCountGreaterThan(4);
    }

    [Fact]
    public void Generate_DecidesOlderRequestsMoreOftenThanNewOnes()
    {
        // Arrange
        var lastMonth = Data.Requests.Where(r => r.SubmittedAt >= StartOfToday.AddDays(-14)).ToList();
        var lastYear = Data.Requests.Where(r => r.SubmittedAt < StartOfToday.AddDays(-180)).ToList();

        // Act
        static double Undecided(IReadOnlyList<IntakeRequestModel> requests) =>
            requests.Count(r => r.Status is "Submitted" or "In Review" or "Waiting on Requester") / (double)requests.Count;

        // Assert
        Undecided(lastMonth).Should().BeGreaterThan(Undecided(lastYear));
    }

    [Fact]
    public void Generate_GivesEveryNeedByItsBasisAndReference()
    {
        // Arrange & Act
        var withNeedBy = Data.Requests.Where(r => r.NeedByDate is not null).ToList();

        // Assert — a commitment records who made it and when; any other basis carries neither
        withNeedBy.Should().NotBeEmpty();
        withNeedBy.Should().OnlyContain(r => r.NeedByBasis != null && !string.IsNullOrWhiteSpace(r.NeedByReference));
        withNeedBy.Where(r => r.NeedByBasis == "Commitment")
            .Should().OnlyContain(r => r.NeedByCommittedByEmployeeNumber != null && r.NeedByCommittedOn != null);
        withNeedBy.Where(r => r.NeedByBasis != "Commitment")
            .Should().OnlyContain(r => r.NeedByCommittedByEmployeeNumber == null && r.NeedByCommittedOn == null);
        Data.Requests.Where(r => r.ChannelName == "Regulatory and compliance").Should().OnlyContain(r => r.NeedByBasis == "Regulatory");
    }

    [Fact]
    public void Generate_TagsOnlyWithTheAxesTagsAndOneSizeEach()
    {
        // Arrange
        var known = Data.TagCategories.SelectMany(c => c.Tags.Select(t => $"{c.Name}|{t}")).ToHashSet();

        // Act
        var tagged = Data.Requests.Where(r => r.Tags.Count > 0).ToList();

        // Assert
        tagged.Should().NotBeEmpty();
        tagged.SelectMany(r => r.Tags).Should().OnlyContain(t => known.Contains(t));
        tagged.Should().OnlyContain(r => r.Tags.Count(t => t.StartsWith("Size|")) == 1);
        Data.Requests.Where(r => r.Status == "Submitted").Should().OnlyContain(r => r.Tags.Count == 0);
    }

    [Fact]
    public void Generate_GivesEveryOriginItsOwnExternalId()
    {
        // Arrange & Act
        var origins = Data.Requests.Where(r => r.OriginSourceSystem is not null).ToList();

        // Assert — a source system and external id identify at most one request
        origins.Should().NotBeEmpty();
        origins.Select(r => (r.OriginSourceSystem, r.OriginExternalId)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_KeepsEveryRowWithinTheImportsLimits()
    {
        // Arrange & Act
        var requests = Data.Requests;

        // Assert
        requests.Select(r => r.Handle).Should().OnlyHaveUniqueItems();
        requests.Should().OnlyContain(r => r.Title.Length <= 256 && !string.IsNullOrWhiteSpace(r.Description));
        requests.Should().OnlyContain(r => r.StatusReason == null || r.StatusReason.Length <= 1024);
        requests.Should().OnlyContain(r => r.NeedByReference == null || r.NeedByReference.Length <= 1024);
    }

    /// <summary>
    /// Org shapes and seeds the single default fixture does not reach: a large company whose value streams
    /// can share a domain, and a startup with no IT or Legal staff to run the fixed desks.
    /// </summary>
    public static TheoryData<int, int, int> Shapes()
    {
        TheoryData<int, int, int> shapes = [];
        foreach (var seed in Enumerable.Range(1, 12))
        {
            shapes.Add(6, 60, seed);
            shapes.Add(1, 4, seed);
        }

        return shapes;
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Generate_KeepsDeskNamesUniqueAndStaffsRequestsWithPeopleEmployedOnTheDay(int valueStreams, int teams, int seed)
    {
        // Arrange
        var (org, data) = Generate(orgOptions: new OrgOptions { ValueStreams = valueStreams, Teams = teams }, seed: seed);
        var employees = org.Employees.ToDictionary(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase);

        bool EmployedOn(string number, DateOnly day) =>
            DateOnly.FromDateTime(employees[number].HireDate!.Value) <= day
            && (org.Structure.TenureOf(number)?.LeftOn is not { } left || left > day);

        // Act
        var desks = data.Desks.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

        // Assert — the import records the assignment and a commitment as that person's, on the day submitted
        foreach (var request in data.Requests)
        {
            var day = DateOnly.FromDateTime(request.SubmittedAt.UtcDateTime);
            desks.Should().ContainKey(request.DeskName);
            EmployedOn(request.RequesterEmployeeNumber, day).Should().BeTrue($"{request.Handle}'s requester should work here on {day}");

            if (request.AssigneeEmployeeNumber is { } assignee)
            {
                desks[request.DeskName].Members.Should().Contain(m => m.EmployeeNumber == assignee);
                EmployedOn(assignee, day).Should().BeTrue($"{request.Handle}'s assignee should work here on {day}");
            }

            if (request.NeedByCommittedByEmployeeNumber is { } committer)
                EmployedOn(committer, day).Should().BeTrue($"{request.Handle}'s commitment should be made by someone working here on {day}");
        }
    }

    [Fact]
    public void Generate_ScalesTheRequestsWithTheMonthlyRate()
    {
        // Arrange & Act
        var few = Generate(new DemandManagementOptions { RequestsPerMonth = 2 }).Data.Requests.Count;
        var many = Generate(new DemandManagementOptions { RequestsPerMonth = 20 }).Data.Requests.Count;

        // Assert
        many.Should().BeGreaterThan(few * 5);
        Generate(new DemandManagementOptions { RequestsPerMonth = 0 }).Data.Requests.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ProducesTheSameDataForTheSameSeed()
    {
        // Arrange & Act
        var first = Generate(seed: 99).Data;
        var second = Generate(seed: 99).Data;

        // Assert
        second.Requests.Select(r => (r.Handle, r.Title, r.Status, r.SubmittedAt))
            .Should().Equal(first.Requests.Select(r => (r.Handle, r.Title, r.Status, r.SubmittedAt)));
    }
}
