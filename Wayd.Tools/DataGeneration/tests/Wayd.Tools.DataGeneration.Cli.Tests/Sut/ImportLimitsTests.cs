using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Client;
using Wayd.Tools.DataGeneration.Cli.Seeding;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

public class ImportLimitsTests
{
    private static ImportLimits Limits(params (string Key, int MaxRows)[] definitions) =>
        new(definitions.Select(d => new ImportDefinitionDto { Key = d.Key, DisplayName = d.Key, MaxRows = d.MaxRows, CanSubmit = true }));

    [Fact]
    public void Require_FailsForAnImportTheTokenCanViewButNotSubmit()
    {
        // Arrange — oversight of imports lists every type, so being listed is not being allowed to post
        var limits = new ImportLimits(
        [
            new ImportDefinitionDto { Key = "ppm.project-tasks", DisplayName = "Project Tasks", MaxRows = 10_000, CanSubmit = false },
        ]);

        // Act
        var act = () => limits.Require(["ppm.project-tasks"]);

        // Assert
        act.Should().Throw<SeedException>().WithMessage("*'ppm.project-tasks'*");
    }

    [Fact]
    public void MaxRows_AnswersWithTheCapTheServerPublished()
    {
        // Arrange
        var limits = Limits(("ppm.project-tasks", 10_000), ("employees", 50_000));

        // Act
        var maxRows = limits.MaxRows("employees");

        // Assert
        maxRows.Should().Be(50_000);
    }

    [Fact]
    public void MaxRows_FailsNamingAKeyTheServerDidNotPublish()
    {
        // Arrange
        var limits = Limits(("employees", 50_000));

        // Act
        var act = () => limits.MaxRows("planning.risks");

        // Assert — no fallback: a guessed cap is the number this exists to stop the seed keeping
        act.Should().Throw<SeedException>().WithMessage("*'planning.risks'*");
    }

    [Fact]
    public void Require_NamesEveryMissingKeyAtOnce()
    {
        // Arrange
        var limits = Limits(("ppm.project-tasks", 10_000));

        // Act
        var act = () => limits.Require(["ppm.project-tasks", "planning.risks", "product-management.deployments"]);

        // Assert — one failure listing both, so fixing a token's permissions takes one attempt
        act.Should().Throw<SeedException>()
            .Where(e => e.Message.Contains("'planning.risks'") && e.Message.Contains("'product-management.deployments'"))
            .Where(e => !e.Message.Contains("'ppm.project-tasks'"));
    }

    [Fact]
    public void Require_PassesWhenEveryKeyIsPublished()
    {
        // Arrange
        var limits = Limits(("ppm.project-tasks", 10_000), ("planning.risks", 10_000));

        // Act
        var act = () => limits.Require(["ppm.project-tasks", "planning.risks"]);

        // Assert
        act.Should().NotThrow();
    }
}
