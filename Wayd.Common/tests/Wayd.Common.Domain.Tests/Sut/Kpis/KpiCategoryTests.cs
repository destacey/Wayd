using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Tests.Sut.Kpis;

public sealed class KpiCategoryTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 16, 9, 30);
    private static readonly EventActor Actor = EventActor.User("user-1", Guid.NewGuid());

    private static KpiCategory NewCategory(string name = "Flow", int order = 0)
    {
        var category = KpiCategory.Create(name, null, order, Actor, Now).Value;
        category.ClearDomainEvents();
        return category;
    }

    [Fact]
    public void Create_ShouldDefineAnActiveCategory_AndRaiseCreated()
    {
        // Arrange — nothing

        // Act
        var result = KpiCategory.Create(" Quality ", " Escaped defects and rework ", 2, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var category = result.Value;
        category.Name.Should().Be("Quality");
        category.IsActive.Should().BeTrue();
        var created = category.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiCategoryCreatedEvent>().Subject;
        (created.Name, created.Description, created.Order).Should().Be(("Quality", "Escaped defects and rework", 2));
    }

    [Fact]
    public void Create_ShouldFail_WithANameTooLong()
    {
        // Arrange
        var name = new string('x', KpiCategory.NameMaxLength + 1);

        // Act
        var result = KpiCategory.Create(name, null, 0, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseBothEnds_OnlyWhenSomethingChanged()
    {
        // Arrange
        var category = NewCategory();

        // Act
        var unchanged = category.UpdateDetails(" Flow ", null, Actor, Now);
        var eventsAfterUnchanged = category.DomainEvents.Count;
        var changed = category.UpdateDetails("Delivery flow", "How work moves", Actor, Now);

        // Assert
        unchanged.IsSuccess.Should().BeTrue();
        eventsAfterUnchanged.Should().Be(0);
        changed.IsSuccess.Should().BeTrue();
        var updated = category.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiCategoryDetailsUpdatedEvent>().Subject;
        (updated.PreviousName, updated.Name, updated.Description).Should().Be(("Flow", "Delivery flow", "How work moves"));
    }

    [Fact]
    public void Deactivate_AndActivate_ShouldToggle_AndRefuseARepeat()
    {
        // Arrange
        var category = NewCategory();

        // Act
        var deactivated = category.Deactivate(Actor, Now);
        var deactivatedAgain = category.Deactivate(Actor, Now);
        var activated = category.Activate(Actor, Now);
        var activatedAgain = category.Activate(Actor, Now);

        // Assert
        deactivated.IsSuccess.Should().BeTrue();
        deactivatedAgain.IsFailure.Should().BeTrue();
        activated.IsSuccess.Should().BeTrue();
        activatedAgain.IsFailure.Should().BeTrue();
        category.IsActive.Should().BeTrue();
        category.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(KpiCategoryDeactivatedEvent), typeof(KpiCategoryActivatedEvent));
    }

    [Fact]
    public void Reorder_ShouldRaiseOnlyForTheCategoriesThatMoved()
    {
        // Arrange
        var flow = NewCategory("Flow", 0);
        var quality = NewCategory("Quality", 1);
        var people = NewCategory("People", 2);

        // Act
        var result = KpiCategory.Reorder([flow, quality, people], [quality.Id, flow.Id, people.Id], Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (flow.Order, quality.Order, people.Order).Should().Be((1, 0, 2));
        people.DomainEvents.Should().BeEmpty();
        var moved = quality.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiCategoryReorderedEvent>().Subject;
        (moved.FromOrder, moved.ToOrder).Should().Be((1, 0));
    }

    [Fact]
    public void Reorder_ShouldFail_WhenTheOrderIsMissingACategory()
    {
        // Arrange
        var flow = NewCategory("Flow", 0);
        var quality = NewCategory("Quality", 1);

        // Act
        var result = KpiCategory.Reorder([flow, quality], [quality.Id], Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        (flow.Order, quality.Order).Should().Be((0, 1));
    }

    [Fact]
    public void Delete_ShouldRaiseDeletedWithTheName()
    {
        // Arrange
        var category = NewCategory("People");

        // Act
        category.Delete(Actor, Now);

        // Assert
        category.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiCategoryDeletedEvent>()
            .Which.Name.Should().Be("People");
    }
}
