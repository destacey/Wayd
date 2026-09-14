using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Risks;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Domain.Tests.Sut.Models;

public sealed class RiskTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 2, 14, 0);
    private static readonly EventActor Actor = EventActor.User("user-1", Guid.CreateVersion7());

    private readonly RiskFaker _riskFaker = new();

    private static Risk Existing(RiskFaker faker)
    {
        var risk = faker.Generate();
        risk.ClearDomainEvents();
        return risk;
    }

    private static Risk UpdateWith(Risk risk,
        string? summary = null,
        string? description = null,
        RiskStatus? status = null,
        RiskCategory? category = null,
        RiskGrade? impact = null,
        RiskGrade? likelihood = null,
        Guid? assigneeId = null,
        bool clearAssignee = false,
        LocalDate? followUpDate = null,
        string? response = null,
        Instant? timestamp = null)
    {
        var result = risk.Update(
            summary ?? risk.Summary,
            description ?? risk.Description,
            status ?? risk.Status,
            category ?? risk.Category,
            impact ?? risk.Impact,
            likelihood ?? risk.Likelihood,
            clearAssignee ? null : assigneeId ?? risk.AssigneeId,
            followUpDate ?? risk.FollowUpDate,
            response ?? risk.Response,
            Actor,
            timestamp ?? Now);

        result.IsSuccess.Should().BeTrue();
        return risk;
    }

    [Fact]
    public void Create_RaisesCreatedEventOnceTheKeyIsAssigned()
    {
        // Arrange
        var teamId = Guid.CreateVersion7();
        var reportedById = Guid.CreateVersion7();

        // Act
        var risk = Risk.Create(" Vendor slip ", "Late parts", teamId, Now, reportedById, RiskCategory.Owned,
            RiskGrade.High, RiskGrade.Medium, null, new LocalDate(2026, 3, 9), null, Actor, Now);

        // Assert
        risk.DomainEvents.Should().BeEmpty("the key is assigned by the first save");

        risk.SetPrivate(r => r.Key, 17);
        risk.ExecutePostPersistenceActions();

        var created = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskCreatedEvent>().Subject;
        created.Id.Should().Be(risk.Id);
        created.Key.Should().Be(17);
        created.Summary.Should().Be("Vendor slip");
        created.TeamId.Should().Be(teamId);
        created.ReportedById.Should().Be(reportedById);
        created.Status.Should().Be(RiskStatus.Open);
        created.Impact.Should().Be(RiskGrade.High);
        created.Likelihood.Should().Be(RiskGrade.Medium);
        created.FollowUpDate.Should().Be(new LocalDate(2026, 3, 9));
        created.Actor.Should().Be(Actor);
        created.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void Create_ThenChangedBeforeTheFirstSave_CreatedEventStillDescribesTheCreation()
    {
        // Arrange
        var risk = Risk.Create("Vendor slip", null, Guid.CreateVersion7(), Now, Guid.CreateVersion7(), RiskCategory.Owned,
            RiskGrade.Low, RiskGrade.Low, null, null, null, Actor, Now);

        // Act
        UpdateWith(risk, summary: "Vendor slipped", status: RiskStatus.Closed);
        risk.SetPrivate(r => r.Key, 5);
        risk.ExecutePostPersistenceActions();

        // Assert
        var created = risk.DomainEvents.OfType<RiskCreatedEvent>().Should().ContainSingle().Subject;
        created.Summary.Should().Be("Vendor slip");
        created.Status.Should().Be(RiskStatus.Open);
        created.ClosedDate.Should().BeNull();

        risk.DomainEvents.OfType<RiskDetailsUpdatedEvent>().Should().ContainSingle().Which.Key.Should().Be(5);
        risk.DomainEvents.OfType<RiskClosedEvent>().Should().ContainSingle().Which.Key.Should().Be(5);
    }

    [Fact]
    public void Import_RecordsTheImportedStatusAsTheCreation()
    {
        // Arrange
        var closedDate = Now.Minus(Duration.FromDays(3));

        // Act
        var risk = Risk.Import("Vendor slip", null, Guid.CreateVersion7(), Now.Minus(Duration.FromDays(10)), Guid.CreateVersion7(),
            RiskStatus.Closed, RiskCategory.Resolved, RiskGrade.Low, RiskGrade.Low, null, null, "Swapped vendor", closedDate,
            EventActor.Import("user-1"), Now);
        risk.SetPrivate(r => r.Key, 3);
        risk.ExecutePostPersistenceActions();

        // Assert
        var created = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskCreatedEvent>().Subject;
        created.Status.Should().Be(RiskStatus.Closed);
        created.ClosedDate.Should().Be(closedDate);
        created.Response.Should().Be("Swapped vendor");
        created.Actor.Kind.Should().Be(EventActorKind.Import);
    }

    [Fact]
    public void Update_WrittenDetailsChanged_RaisesDetailsUpdatedWithWhatItReplaced()
    {
        // Arrange
        var risk = Existing(_riskFaker.WithSummary("Vendor slip").WithDescription(null).WithResponse("Escalated"));

        // Act
        UpdateWith(risk, summary: "Vendor slipped", response: "Swapped vendor");

        // Assert
        var raised = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskDetailsUpdatedEvent>().Subject;
        raised.Summary.Should().Be("Vendor slipped");
        raised.Response.Should().Be("Swapped vendor");
        raised.Previous.Should().Be(new RiskDetails("Vendor slip", null, "Escalated"));
    }

    [Fact]
    public void Update_TextThatOnlyDiffersByWhitespace_RaisesNothing()
    {
        // Arrange
        var risk = Existing(_riskFaker.WithSummary("Vendor slip").WithResponse("Escalated"));

        // Act
        UpdateWith(risk, summary: " Vendor slip ", response: "Escalated ");

        // Assert
        risk.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_NothingChanged_RaisesNothing()
    {
        // Arrange
        var risk = Existing(_riskFaker);

        // Act
        UpdateWith(risk);

        // Assert
        risk.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_CategoryChanged_RaisesCategoryChanged()
    {
        // Arrange
        var risk = Existing(_riskFaker.WithCategory(RiskCategory.Owned));

        // Act
        UpdateWith(risk, category: RiskCategory.Mitigated);

        // Assert
        var raised = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskCategoryChangedEvent>().Subject;
        raised.PreviousCategory.Should().Be(RiskCategory.Owned);
        raised.Category.Should().Be(RiskCategory.Mitigated);
    }

    [Fact]
    public void Update_Reassessed_CarriesBothGradesAndTheExposureAtBothEnds()
    {
        // Arrange
        var risk = Existing(_riskFaker.WithImpact(RiskGrade.Low).WithLikelihood(RiskGrade.Low));

        // Act
        UpdateWith(risk, impact: RiskGrade.High, likelihood: RiskGrade.Medium);

        // Assert
        var raised = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskAssessmentChangedEvent>().Subject;
        raised.PreviousImpact.Should().Be(RiskGrade.Low);
        raised.PreviousLikelihood.Should().Be(RiskGrade.Low);
        raised.PreviousExposure.Should().Be(RiskGrade.Low);
        raised.Impact.Should().Be(RiskGrade.High);
        raised.Likelihood.Should().Be(RiskGrade.Medium);
        raised.Exposure.Should().Be(RiskGrade.High);
    }

    [Fact]
    public void Update_Unassigned_RaisesAssigneeChanged()
    {
        // Arrange
        var assigneeId = Guid.CreateVersion7();
        var risk = Existing(_riskFaker.WithAssigneeId(assigneeId));

        // Act
        UpdateWith(risk, clearAssignee: true);

        // Assert
        var raised = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskAssigneeChangedEvent>().Subject;
        raised.PreviousAssigneeId.Should().Be(assigneeId);
        raised.AssigneeId.Should().BeNull();
    }

    [Fact]
    public void Update_FollowUpDateMoved_RaisesFollowUpDateChanged()
    {
        // Arrange
        var risk = Existing(_riskFaker.WithFollowUpDate(new LocalDate(2026, 3, 9)));

        // Act
        UpdateWith(risk, followUpDate: new LocalDate(2026, 3, 16));

        // Assert
        var raised = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskFollowUpDateChangedEvent>().Subject;
        raised.PreviousFollowUpDate.Should().Be(new LocalDate(2026, 3, 9));
        raised.FollowUpDate.Should().Be(new LocalDate(2026, 3, 16));
    }

    [Fact]
    public void Update_Closed_RaisesClosedWithTheClosedDate()
    {
        // Arrange
        var risk = Existing(_riskFaker.AsOpen());

        // Act
        UpdateWith(risk, status: RiskStatus.Closed);

        // Assert
        var raised = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskClosedEvent>().Subject;
        raised.ClosedDate.Should().Be(Now);
        risk.ClosedDate.Should().Be(Now);
    }

    [Fact]
    public void Update_Reopened_RaisesReopenedWithTheClosedDateItCleared()
    {
        // Arrange
        var closedDate = Now.Minus(Duration.FromDays(2));
        var risk = Existing(_riskFaker.AsClosed(closedDate));

        // Act
        UpdateWith(risk, status: RiskStatus.Open);

        // Assert
        var raised = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskReopenedEvent>().Subject;
        raised.PreviousClosedDate.Should().Be(closedDate);
        risk.ClosedDate.Should().BeNull();
    }
}
