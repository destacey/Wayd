using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class WorkflowScopeChainTests
{
    [Fact]
    public void Of_ShouldKeepTheScopesInOrder_MostSpecificFirst()
    {
        // Arrange
        var (project, program, portfolio) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var chain = WorkflowScopeChain.Of(project, program, portfolio);

        // Assert
        chain.ScopeIds.Should().Equal(project, program, portfolio);
        chain.Rank(project).Should().Be(0);
        chain.Rank(portfolio).Should().Be(2);
    }

    [Fact]
    public void Rank_ShouldPutTheOrganizationAfterEveryScope()
    {
        // Arrange
        var chain = WorkflowScopeChain.Of(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var organization = chain.Rank(null);

        // Assert
        organization.Should().Be(2);
        chain.Rank(Guid.NewGuid()).Should().BeGreaterThan(organization);
    }

    [Fact]
    public void ForScope_ShouldBeTheOrganizationChain_WhenThereIsNoScope()
    {
        // Act
        var chain = WorkflowScopeChain.ForScope(null);

        // Assert
        chain.Should().BeSameAs(WorkflowScopeChain.Organization);
        chain.ScopeIds.Should().BeEmpty();
    }

    [Fact]
    public void ForScope_ShouldBeAChainOfOne_WhenThereIsAScope()
    {
        // Arrange
        var scopeId = Guid.NewGuid();

        // Act
        var chain = WorkflowScopeChain.ForScope(scopeId);

        // Assert
        chain.ScopeIds.Should().Equal(scopeId);
    }

    [Fact]
    public void Of_ShouldBeTheOrganizationChain_WhenGivenNoScopes()
    {
        // Act
        var chain = WorkflowScopeChain.Of([]);

        // Assert
        chain.Should().BeSameAs(WorkflowScopeChain.Organization);
    }

    [Fact]
    public void ScopeIds_ShouldNotExposeTheChainsOwnStorage()
    {
        // Arrange
        var chain = WorkflowScopeChain.Of(Guid.NewGuid());

        // Act
        var act = () => ((IList<Guid>)chain.ScopeIds)[0] = Guid.NewGuid();

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Of_ShouldThrow_WhenAScopeIsEmpty()
    {
        // Act
        var act = () => WorkflowScopeChain.Of(Guid.NewGuid(), Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Of_ShouldThrow_WhenAScopeIsListedTwice()
    {
        // Arrange
        var scopeId = Guid.NewGuid();

        // Act
        var act = () => WorkflowScopeChain.Of(scopeId, Guid.NewGuid(), scopeId);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
