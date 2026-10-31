using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Application.Products.Imports;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Imports;

/// <summary>
/// Importing a catalog of products. The definition's own work is ordering the rows so parents precede
/// children, resolving types, statuses and tags, and rejecting any row whose references it cannot honour.
/// </summary>
public sealed class ProductImportDefinitionTests
{
    private const int CreatePass = 0;

    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Mock<IDispatcher> _dispatcher = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly StatusWorkflow _workflow;
    private readonly WorkflowStatus _concept;
    private readonly ProductImportDefinition _definition;

    public ProductImportDefinitionTests()
    {
        ProductWorkflowOwners.Register();

        _currentUser.Setup(u => u.GetUserId()).Returns(_userId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        _workflow = StatusWorkflow.CreateSystem("Product Lifecycle", null, ProductWorkflowOwners.Product.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        _concept = _workflow.AddSystemStatus("Concept", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        _workflow.AddSystemStatus("Active", null, StatusCategory.Active, (int)ProductStatusAlias.Active, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        _workflow.AddSystemStatus("Retired", null, StatusCategory.Done, (int)ProductStatusAlias.Retired, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        _workflow.PublishSystem(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_workflow));

        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_concept)));

        _definition = new ProductImportDefinition(
            _dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            _dateTimeProvider.Object,
            Mock.Of<ILogger<ProductImportDefinition>>(),
            new ImportPayloadSerializer(),
            _dispatcher.Object);

        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsByIdOrCodeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTeamsByIdOrCodeQuery q, CancellationToken _) => (IReadOnlyList<ISimpleTeam>)[.. _organizationTeams
                .Where(t => q.Ids.Contains(t.Id) || q.Codes.Contains(t.Code.Value, StringComparer.OrdinalIgnoreCase))]);

        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleTeamQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetSimpleTeamQuery q, CancellationToken _) => _organizationTeams.FirstOrDefault(t => t.Id == q.Id));
    }

    /// <summary>The teams Organization holds, which codes resolve against and missing copies are built from.</summary>
    private readonly List<TeamSource> _organizationTeams = [];

    /// <summary>A team as Organization holds it, for building the module's copy.</summary>
    private sealed record TeamSource(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;

    /// <summary>
    /// A team Organization holds, and — unless <paramref name="copied"/> is false — the module's copy of it, as the
    /// sync leaves it.
    /// </summary>
    private TeamSource SeedTeam(string name = "Payments", string code = "PAY", bool isActive = true, bool copied = true)
    {
        var source = new TeamSource(Guid.CreateVersion7(), _organizationTeams.Count + 1, name, new TeamCode(code), TeamType.Team, isActive);
        _organizationTeams.Add(source);

        if (copied)
            _dbContext.AddProductTeam(new ProductTeam(source, Now));

        return source;
    }

    /// <summary>
    /// Changes a team in Organization without touching the module's copy, as a change not yet synced leaves it.
    /// </summary>
    private TeamSource ChangeInOrganization(TeamSource team, string? name = null, string? code = null, bool? isActive = null)
    {
        var changed = team with
        {
            Name = name ?? team.Name,
            Code = code is null ? team.Code : new TeamCode(code),
            IsActive = isActive ?? team.IsActive,
        };
        _organizationTeams[_organizationTeams.IndexOf(team)] = changed;

        return changed;
    }

    private ProductType SeedType(string name = "Application", bool isReleasable = true, bool isActive = true)
    {
        var productType = ProductType.Create(name, null, isReleasable, 1);

        if (!isActive)
        {
            productType.Deactivate();
        }

        _dbContext.AddProductType(productType);

        return productType;
    }

    private (ProductTagCategory Category, ProductTag Tag) SeedTag(
        string categoryName = "Platform",
        string tagName = "ios",
        bool allowsMany = true,
        bool categoryActive = true,
        bool tagActive = true)
    {
        var category = ProductTagCategory.Create(categoryName, null, allowsMany, 1, EventActor.System, Now);
        var tag = category.AddTag(tagName, null, EventActor.System, Now).Value;

        if (!tagActive)
        {
            category.DeactivateTag(tag.Id, EventActor.System, Now);
        }

        if (!categoryActive)
        {
            category.Deactivate(EventActor.System, Now);
        }

        category.ClearDomainEvents();
        _dbContext.AddProductTagCategory(category);
        _dbContext.AddProductTag(tag);

        return (category, tag);
    }

    /// <summary>Rows keyed by the import id each one supplies, so a child can name its parent.</summary>
    private ImportProcessRow[] Rows((string ImportId, ImportProductDto Data)[] rows) =>
        [.. rows.Select((r, i) => ImportProcessRow.Create(r.ImportId, i + 1, _definition.SerializeRow(r.Data)))];

    private Task<Result<ImportPassResult>> Run(params (string ImportId, ImportProductDto Data)[] rows) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(rows), isFinalChunk: true, TestContext.Current.CancellationToken);

    private static (string, ImportProductDto) Row(
        string importId,
        string name,
        string productTypeName = "Application",
        string? parentImportId = null,
        string? status = null,
        string? description = null,
        string? externalId = null,
        (string Category, string Tag)[]? tags = null,
        string[]? teams = null) =>
        (importId,
            new ImportProductDto(name, description, productTypeName, parentImportId, externalId, status,
                [.. (tags ?? []).Select(t => new ProductTagReference(t.Category, t.Tag))],
                teams));

    private IReadOnlyList<string?> GroupKeys(params (string ImportId, ImportProductDto Data)[] rows) =>
        _definition.GroupKeysOf([.. rows.Select(r => (r.ImportId, _definition.SerializeRow(r.Data)))]);

    [Fact]
    public void Definition_AppliesTreeByTree()
    {
        // Arrange & Act & Assert — chunked, which is safe only because a tree is one group and the runner
        // never splits a group
        _definition.Atomicity.Should().Be(ImportAtomicity.PerGroup);
        _definition.GroupNoun.Should().Be("product tree");
        _definition.Passes.Single().Scope.Should().Be(ImportPassScope.Chunked);
    }

    [Fact]
    public void GroupKeysOf_IsTheRootOfEachRowsTree_WhateverOrderTheFileListsThem()
    {
        // Act — a grandchild ahead of its parent and root, and a second tree interleaved
        var keys = GroupKeys(
            Row("web", "Storefront Web", parentImportId: "sf"),
            Row("cart", "Cart", parentImportId: "web"),
            Row("id", "Identity"),
            Row("sf", "Storefront"),
            Row("sso", "Single sign-on", parentImportId: "id"));

        // Assert
        keys.Should().Equal("sf", "sf", "id", "sf", "id");
    }

    [Fact]
    public void GroupKeysOf_IsTheRootAsItsOwnRowSpellsIt()
    {
        // Act — the child names its parent in different casing, which the pass resolves; the group must still
        // be one string for the whole tree
        var keys = GroupKeys(
            Row("Storefront", "Storefront"),
            Row("web", "Storefront Web", parentImportId: " STOREFRONT "));

        // Assert
        keys.Should().Equal("Storefront", "Storefront");
    }

    [Fact]
    public void GroupKeysOf_Terminates_OnACycleTheSubmissionWouldRefuse()
    {
        // Act
        var keys = GroupKeys(
            Row("a", "A", parentImportId: "b"),
            Row("b", "B", parentImportId: "a"));

        // Assert
        keys.Should().HaveCount(2).And.OnlyContain(k => k == "a" || k == "b");
    }

    [Fact]
    public async Task CreateProducts_CreatesEveryRowInTheFile()
    {
        // Arrange
        SeedType();

        // Act
        var result = await Run(Row("1", "Wayd"), Row("2", "Wayd API"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.Products.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateProducts_ReportsTheProductItCreatedAgainstTheRow()
    {
        // Arrange
        SeedType();

        // Act
        var result = await Run(Row("1", "Wayd"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.CreatedEntityId.Should().NotBeNull();
        _dbContext.Products.Single().Id.Should().Be(outcome.CreatedEntityId!.Value);
    }

    [Fact]
    public async Task CreateProducts_LinksAChildListedBeforeItsParent()
    {
        // Arrange — the child is listed first, which is the case creating in file order would break
        SeedType();

        // Act
        var result = await Run(
            Row("2", "Wayd API", parentImportId: "1"),
            Row("1", "Wayd"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        var parent = _dbContext.Products.Single(p => p.Name == "Wayd");
        _dbContext.Products.Single(p => p.Name == "Wayd API").ParentId.Should().Be(parent.Id);
        parent.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task CreateProducts_LinksEveryLevelOfADeepTree()
    {
        // Arrange — deepest first, so the ordering has to walk the whole chain
        SeedType();

        // Act
        var result = await Run(
            Row("3", "Checkout", parentImportId: "2"),
            Row("2", "Wayd API", parentImportId: "1"),
            Row("1", "Wayd"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        var root = _dbContext.Products.Single(p => p.Name == "Wayd");
        var middle = _dbContext.Products.Single(p => p.Name == "Wayd API");
        middle.ParentId.Should().Be(root.Id);
        _dbContext.Products.Single(p => p.Name == "Checkout").ParentId.Should().Be(middle.Id);
    }

    [Fact]
    public async Task CreateProducts_ImportsTwoProductsSharingANameUnderDifferentParents()
    {
        // Arrange — the case that makes the import id necessary: names alone could not tell these apart
        SeedType();

        // Act
        var result = await Run(
            Row("1", "Platform A"),
            Row("2", "Platform B"),
            Row("3", "API", parentImportId: "1"),
            Row("4", "API", parentImportId: "2"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        var apis = _dbContext.Products.Where(p => p.Name == "API").ToList();
        apis.Should().HaveCount(2);
        apis.Select(a => a.ParentId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreateProducts_AppliesTheStatusARowNames()
    {
        // Arrange
        SeedType();

        // Act
        var result = await Run(Row("1", "Wayd", status: "Retired"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Products.Single().StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public async Task CreateProducts_UsesTheWorkflowsInitialStatusWhenARowOmitsOne()
    {
        // Arrange
        SeedType();

        // Act
        var result = await Run(Row("1", "Wayd"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Products.Single().StatusCategory.Should().Be(StatusCategory.Proposed);
    }

    [Fact]
    public async Task CreateProducts_AttributesEveryRowToTheImport()
    {
        // Arrange
        SeedType();

        // Act
        await Run(Row("1", "Wayd"));

        // Assert
        _dbContext.Products.Single().StatusTransitions.Should().ContainSingle()
            .Which.ActorKind.Should().Be(EventActorKind.Import);
    }

    [Fact]
    public async Task CreateProducts_KeepsTheUserWhoRanTheImport()
    {
        // Arrange
        SeedType();

        // Act
        await Run(Row("1", "Wayd"));

        // Assert
        _dbContext.Products.Single().StatusTransitions.Single().ActorUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task CreateProducts_RejectsARowWhoseParentIsNotInTheFile()
    {
        // Arrange — a parent must be a row in this file: the import stands a catalog up rather than
        // grafting onto one, so a product already in the catalog is not a reference it can resolve
        var productType = SeedType();
        _dbContext.AddProduct(Product.Create(
            "Already Here", null, productType.Id, null, null,
            StatusRef.From(_concept), EventActor.System, Now));

        // Act
        var result = await Run(Row("2", "Wayd API", parentImportId: "99"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("'99'").And.Contain("same file");
        _dbContext.Products.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateProducts_RejectsRowsThatFormAParentCycle()
    {
        // Arrange — the submission command rejects a cycle before the run starts; if one reaches the pass,
        // the rows involved can never be ordered parents-first, so each is rejected on its own
        SeedType();

        // Act
        var result = await Run(
            Row("1", "One", parentImportId: "2"),
            Row("2", "Two", parentImportId: "1"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r =>
        {
            r.Failed.Should().BeTrue();
            r.Error.Should().Contain("circular");
        });
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProducts_RejectsARowNamingAProductTypeThatDoesNotExist()
    {
        // Arrange
        SeedType();

        // Act
        var result = await Run(Row("1", "Wayd", productTypeName: "Nonexistent"));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Nonexistent");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProducts_RejectsARowNamingAnInactiveProductType()
    {
        // Arrange — an inactive type cannot be assigned, so it reads the same as one that is not there
        SeedType("Application", isActive: false);

        // Act
        var result = await Run(Row("1", "Wayd"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Application");
    }

    [Fact]
    public async Task CreateProducts_RejectsARowNamingAStatusOutsideTheWorkflow()
    {
        // Arrange
        SeedType();

        // Act
        var result = await Run(Row("1", "Wayd", status: "Shipped"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Shipped");
    }

    [Fact]
    public async Task CreateProducts_ResolvesReferencesDifferingOnlyByCaseAndWhitespace()
    {
        // Arrange
        SeedType();

        // Act
        var result = await Run(
            Row("1", "Wayd", productTypeName: " application "),
            Row("2", "Wayd API", productTypeName: "APPLICATION", parentImportId: " 1 "));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        var parent = _dbContext.Products.Single(p => p.Name == "Wayd");
        _dbContext.Products.Single(p => p.Name == "Wayd API").ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task CreateProducts_AppliesTheTagsARowNames()
    {
        // Arrange
        SeedType();
        var (category, ios) = SeedTag();
        var android = category.AddTag("android", null, EventActor.System, Now).Value;
        _dbContext.AddProductTag(android);

        // Act
        var result = await Run(Row("1", "Wayd Client", tags: [("Platform", "ios"), ("Platform", "android")]));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Products.Single().Tags.Select(t => t.TagId).Should().BeEquivalentTo([ios.Id, android.Id]);
    }

    [Fact]
    public async Task CreateProducts_ResolvesTheSameTagNameOnTwoAxesToItsOwnAxis()
    {
        // Arrange — the reason a tag reference carries its axis: 'gold' alone identifies neither of these
        SeedType();
        var (_, supportGold) = SeedTag("Support", "gold");
        var (_, tierGold) = SeedTag("Tier", "gold");

        // Act
        var result = await Run(
            Row("1", "Alpha", tags: [("Support", "gold")]),
            Row("2", "Beta", tags: [("Tier", "gold")]));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.Products.Single(p => p.Name == "Alpha").Tags.Single().TagId.Should().Be(supportGold.Id);
        _dbContext.Products.Single(p => p.Name == "Beta").Tags.Single().TagId.Should().Be(tierGold.Id);
    }

    [Fact]
    public async Task CreateProducts_KeepsTheLastTagWhenTheAxisAllowsOne()
    {
        // Arrange
        SeedType();
        var (category, _) = SeedTag(allowsMany: false);
        var android = category.AddTag("android", null, EventActor.System, Now).Value;
        _dbContext.AddProductTag(android);

        // Act
        var result = await Run(Row("1", "Wayd Client", tags: [("Platform", "ios"), ("Platform", "android")]));

        // Assert — the aggregate treats a second tag on a single-value axis as a correction rather than an
        // error, so the row is imported carrying the last one it named
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Products.Single().Tags.Should().ContainSingle().Which.TagId.Should().Be(android.Id);
    }

    [Theory]
    [InlineData("Nonexistent", "ios", "Nonexistent")]
    [InlineData("Platform", "windows", "windows")]
    public async Task CreateProducts_RejectsARowNamingATagThatCannotBeResolved(
        string categoryName, string tagName, string expected)
    {
        // Arrange
        SeedType();
        SeedTag();

        // Act
        var result = await Run(Row("1", "Wayd", tags: [(categoryName, tagName)]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(expected);
        _dbContext.Products.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CreateProducts_RejectsARowNamingAnInactiveTagOrAxis(bool tagActive, bool categoryActive)
    {
        // Arrange — an inactive tag or axis cannot be applied, so it reads the same as one that is not there
        SeedType();
        SeedTag(tagActive: tagActive, categoryActive: categoryActive);

        // Act
        var result = await Run(Row("1", "Wayd", tags: [("Platform", "ios")]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Platform|ios");
    }

    [Fact]
    public async Task CreateProducts_LeavesAProductUntaggedWhenTheRowNamesNoTags()
    {
        // Arrange
        SeedType();
        SeedTag();

        // Act
        var result = await Run(Row("1", "Wayd"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Products.Single().Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProducts_LinksTheTeamsARowNames_ByCodeOrId()
    {
        // Arrange
        SeedType();
        var payments = SeedTeam("Payments", "PAY");
        var mobile = SeedTeam("Mobile", "MOB");

        // Act
        var result = await Run(Row("1", "Wayd", teams: ["pay", mobile.Id.ToString()]));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Products.Single().TeamLinks.Select(l => l.TeamId).Should().BeEquivalentTo([payments.Id, mobile.Id]);
    }

    [Fact]
    public async Task CreateProducts_LinksTheTeamsToTheRowsOwnProductOnly()
    {
        // Arrange — nothing rolls up or is inherited: a parent's teams are the parent's alone
        SeedType();
        var payments = SeedTeam("Payments", "PAY");
        var mobile = SeedTeam("Mobile", "MOB");

        // Act
        var result = await Run(
            Row("line", "Wayd", teams: ["PAY"]),
            Row("app", "Wayd Mobile", parentImportId: "line", teams: ["MOB"]));

        // Assert
        result.Value.Rows.Should().OnlyContain(r => !r.Failed);
        _dbContext.Products.Single(p => p.Name == "Wayd").TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(payments.Id);
        _dbContext.Products.Single(p => p.Name == "Wayd Mobile").TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(mobile.Id);
    }

    [Fact]
    public async Task CreateProducts_LinksATeamWithNoCopyYet_ByCopyingItFromOrganization()
    {
        // Arrange — a team created moments ago has no copy in the module until the sync runs
        SeedType();
        var payments = SeedTeam("Payments", "PAY", copied: false);

        // Act
        var result = await Run(Row("1", "Wayd", teams: ["PAY"]));

        // Assert — the copy is saved on its own, before any product is staged
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.ProductTeams.Should().ContainSingle().Which.Id.Should().Be(payments.Id);
        _dbContext.Products.Single().TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(payments.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateProducts_RaisesTheLinkedEventOnlyOnceTheProductIsSaved()
    {
        // Arrange — a new product has no key until it is saved, and the event carries it
        SeedType();
        SeedTeam("Payments", "PAY");

        // Act
        await Run(Row("1", "Wayd", teams: ["PAY"]));

        // Assert
        _dbContext.Products.Single().DomainEvents.OfType<ProductTeamLinkedEvent>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("NOPE")]
    [InlineData("0196b5c4-1f1e-7a52-9d2e-3c4b5a697887")]
    public async Task CreateProducts_RejectsARowNamingATeamThatDoesNotExist(string entry)
    {
        // Arrange
        SeedType();
        SeedTeam("Payments", "PAY");

        // Act
        var result = await Run(Row("1", "Wayd", teams: ["PAY", entry]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Be($"No team was found with the code or ID '{entry}'.");
        _dbContext.Products.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateProducts_RejectsARowNamingAnInactiveTeam(bool copied)
    {
        // Arrange
        SeedType();
        SeedTeam("Legacy", "OLD", isActive: false, copied: copied);

        // Act
        var result = await Run(Row("1", "Wayd", teams: ["OLD"]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Be("The team 'Legacy' (OLD) is inactive, and only an active team can be linked to a product.");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProducts_RejectsATeamDeactivatedInOrganization_BeforeItsCopyHasCaughtUp()
    {
        // Arrange — Organization owns whether a team is active; the module's copy may not have heard yet
        SeedType();
        ChangeInOrganization(SeedTeam("Legacy", "OLD"), isActive: false);

        // Act
        var result = await Run(Row("1", "Wayd", teams: ["OLD"]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Be("The team 'Legacy' (OLD) is inactive, and only an active team can be linked to a product.");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProducts_ResolvesATeamByTheCodeOrganizationHoldsNow_BeforeItsCopyHasCaughtUp()
    {
        // Arrange — a team renamed a moment ago: the file names it the way Organization does today
        SeedType();
        var team = ChangeInOrganization(SeedTeam("Payments", "PAY"), name: "Payments Core", code: "PAYC");

        // Act
        var result = await Run(Row("1", "Wayd", teams: ["PAYC"]), Row("2", "Wayd Legacy", teams: ["PAY"]));

        // Assert
        var outcomes = result.Value.Rows.ToDictionary(r => r.ImportId);
        outcomes["1"].Failed.Should().BeFalse();
        _dbContext.Products.Single().TeamLinks.Should().ContainSingle().Which.TeamId.Should().Be(team.Id);
        outcomes["2"].Error.Should().Be("No team was found with the code or ID 'PAY'.");
    }

    [Fact]
    public async Task CreateProducts_ReadsOrganizationAndTheCopiesOnceForTheWholeChunk()
    {
        // Arrange
        SeedType();
        SeedTeam("Payments", "PAY");
        SeedTeam("Mobile", "MOB");

        // Act
        await Run(Row("1", "Alpha", teams: ["PAY", "MOB"]), Row("2", "Beta", teams: ["MOB"]));

        // Assert
        _dispatcher.Verify(d => d.Send(It.IsAny<GetTeamsByIdOrCodeQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<GetSimpleTeamQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateProducts_LeavesAProductUnlinkedWhenTheRowNamesNoTeams()
    {
        // Arrange
        SeedType();
        SeedTeam();

        // Act
        var result = await Run(Row("1", "Wayd"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Products.Single().TeamLinks.Should().BeEmpty();
        _dispatcher.Verify(d => d.Send(It.IsAny<GetTeamsByIdOrCodeQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SerializedRow_StoredBeforeTheTeamsColumnExisted_ReadsAsNamingNoTeams()
    {
        // Arrange — a run paused before this column shipped resumes with payloads that have no Teams
        const string stored = """{"name":"Wayd","description":null,"productTypeName":"Application","parentImportId":null,"externalId":null,"status":null,"tags":[]}""";

        // Act
        var row = new ImportPayloadSerializer().Deserialize<ImportProductDto>(stored);

        // Assert
        row.Teams.Should().BeNull();
    }
}
