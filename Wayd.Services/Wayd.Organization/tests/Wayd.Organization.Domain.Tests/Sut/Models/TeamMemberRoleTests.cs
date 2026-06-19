using Wayd.Organization.Domain.Models;
using Wayd.Organization.Domain.Tests.Data;

namespace Wayd.Organization.Domain.Tests.Sut.Models;

public class TeamMemberRoleTests
{
    private readonly TeamMemberRoleFaker _faker = new();

    #region Create

    [Fact]
    public void Create_WhenValid_ReturnsSuccess()
    {
        var fake = _faker.Generate();

        var result = TeamMemberRole.Create(fake.Name, fake.Description);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(fake.Name);
        result.Value.Description.Should().Be(fake.Description);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullDescription_ReturnsSuccess()
    {
        var result = TeamMemberRole.Create("Engineer", null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("  leads  ", "leads")]
    [InlineData("Senior Engineer", "Senior Engineer")]
    public void Create_TrimsName(string input, string expected)
    {
        var result = TeamMemberRole.Create(input, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(expected);
    }

    [Theory]
    [InlineData("  has spaces  ", "has spaces")]
    [InlineData("clean", "clean")]
    public void Create_TrimsDescription(string input, string expected)
    {
        var result = TeamMemberRole.Create("Role", input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be(expected);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void Create_WithWhitespaceDescription_SetsDescriptionToNull(string input)
    {
        var result = TeamMemberRole.Create("Role", input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceName_ReturnsFailure(string? name)
    {
        var result = TeamMemberRole.Create(name!, null);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WhenValid_UpdatesNameAndDescription()
    {
        var role = TeamMemberRole.Create("Old Name", "Old description").Value;

        var result = role.Update("New Name", "New description");

        result.IsSuccess.Should().BeTrue();
        role.Name.Should().Be("New Name");
        role.Description.Should().Be("New description");
    }

    [Fact]
    public void Update_WithNullDescription_ClearsDescription()
    {
        var role = TeamMemberRole.Create("Role", "Some description").Value;

        var result = role.Update("Role", null);

        result.IsSuccess.Should().BeTrue();
        role.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void Update_WithWhitespaceDescription_SetsDescriptionToNull(string input)
    {
        var role = TeamMemberRole.Create("Role", "Some description").Value;

        var result = role.Update("Role", input);

        result.IsSuccess.Should().BeTrue();
        role.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithNullOrWhitespaceName_ReturnsFailure(string? name)
    {
        var role = TeamMemberRole.Create("Role", null).Value;

        var result = role.Update(name!, null);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Activate / Deactivate

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var role = _faker.AsInactive().Generate();

        var result = role.Activate();

        result.IsSuccess.Should().BeTrue();
        role.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var role = _faker.AsActive().Generate();

        var result = role.Deactivate();

        result.IsSuccess.Should().BeTrue();
        role.IsActive.Should().BeFalse();
    }

    #endregion
}
