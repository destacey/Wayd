using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Conditions;

public sealed class TransitionConditionCheckerTests
{
    private const string OwnerType = "test.widget";

    private static readonly Guid RecordId = Guid.CreateVersion7();
    private static readonly Guid FromStatusId = Guid.CreateVersion7();

    // A target whose move from FromStatusId needs the given conditions; a move from anywhere else needs nothing.
    private static StatusRef Target(params TransitionConditionKind[] conditions) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Terminal", StatusCategory.Done,
            allowedFrom: new HashSet<Guid> { FromStatusId },
            conditionsFrom: new Dictionary<Guid, IReadOnlySet<TransitionConditionKind>> { [FromStatusId] = conditions.ToHashSet() });

    [Fact]
    public void IsAvailable_ShouldBeTrueForAConditionTheRecordChecks()
    {
        // Arrange
        var sut = new TransitionConditionChecker([]);

        // Act
        var available = sut.IsAvailable(TransitionConditionKind.RequiresReason);

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_ShouldDependOnAnEvaluator_ForAConditionTheRecordCannotCheck()
    {
        // Arrange
        var without = new TransitionConditionChecker([]);
        var with = new TransitionConditionChecker([new FakeConditionEvaluator(TransitionConditionKind.RequiresPassedApproval, isMet: true)]);

        // Act
        var availableWithout = without.IsAvailable(TransitionConditionKind.RequiresPassedApproval);
        var availableWith = with.IsAvailable(TransitionConditionKind.RequiresPassedApproval);

        // Assert
        availableWithout.Should().BeFalse();
        availableWith.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Evaluate_ShouldRecordWhatTheEvaluatorDecided(bool isMet)
    {
        // Arrange
        var evaluator = new FakeConditionEvaluator(TransitionConditionKind.RequiresPassedApproval, isMet);
        var sut = new TransitionConditionChecker([evaluator]);
        var target = Target(TransitionConditionKind.RequiresPassedApproval);

        // Act
        var evaluated = await sut.Evaluate(OwnerType, RecordId, FromStatusId, target, TestContext.Current.CancellationToken);

        // Assert
        evaluated.StatusId.Should().Be(target.StatusId);
        evaluated.MetConditions.Contains(TransitionConditionKind.RequiresPassedApproval).Should().Be(isMet);
        var asked = evaluator.Asked.Should().ContainSingle().Subject;
        (asked.OwnerType, asked.RecordId, asked.FromStatusId).Should().Be((OwnerType, RecordId, FromStatusId));
        asked.Target.Should().BeSameAs(target);
    }

    [Fact]
    public async Task Evaluate_ShouldLeaveAConditionUnmet_WhenNothingEvaluatesIt()
    {
        // Arrange
        var sut = new TransitionConditionChecker([]);
        var target = Target(TransitionConditionKind.RequiresPassedApproval);

        // Act
        var evaluated = await sut.Evaluate(OwnerType, RecordId, FromStatusId, target, TestContext.Current.CancellationToken);

        // Assert
        evaluated.MetConditions.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluate_ShouldNotAskAboutConditionsTheRecordChecks_OrOnOtherMoves()
    {
        // Arrange
        var evaluator = new FakeConditionEvaluator(TransitionConditionKind.RequiresPassedApproval, isMet: true);
        var sut = new TransitionConditionChecker([evaluator]);

        // Act
        var reasonOnly = await sut.Evaluate(
            OwnerType, RecordId, FromStatusId, Target(TransitionConditionKind.RequiresReason), TestContext.Current.CancellationToken);
        var otherMove = await sut.Evaluate(
            OwnerType, RecordId, Guid.CreateVersion7(), Target(TransitionConditionKind.RequiresPassedApproval), TestContext.Current.CancellationToken);

        // Assert
        evaluator.Asked.Should().BeEmpty();
        reasonOnly.MetConditions.Should().BeEmpty("a reason is the record's to check");
        otherMove.MetConditions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ShouldRefuseTwoEvaluatorsForOneKind()
    {
        // Arrange
        ITransitionConditionEvaluator[] evaluators =
        [
            new FakeConditionEvaluator(TransitionConditionKind.RequiresPassedApproval, isMet: true),
            new FakeConditionEvaluator(TransitionConditionKind.RequiresPassedApproval, isMet: false),
        ];

        // Act
        var act = () => new TransitionConditionChecker(evaluators);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("More than one evaluator is registered for RequiresPassedApproval.");
    }

    [Fact]
    public void Constructor_ShouldRefuseAnEvaluatorForAConditionTheRecordChecks()
    {
        // Arrange
        ITransitionConditionEvaluator[] evaluators = [new FakeConditionEvaluator(TransitionConditionKind.RequiresReason, isMet: true)];

        // Act
        var act = () => new TransitionConditionChecker(evaluators);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("RequiresReason is checked by the record; it takes no evaluator.");
    }
}
