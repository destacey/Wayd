using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Domain.Tests.Sut.Identity;

/// <summary>
/// Pins the discriminated-shape invariant on <see cref="RegistrationPolicy"/>: the
/// employee-gate and default-role settings are only observable while
/// auto-registration is enabled, so an illegal "disabled but configured" state is
/// unrepresentable rather than merely discouraged. The invariant is enforced by the
/// constructor (the single chokepoint for state), so the FromFlat cases below — which
/// forward raw inputs straight to it — exercise that enforcement, not a factory's.
/// </summary>
public sealed class RegistrationPolicyTests
{
    [Fact]
    public void Disabled_HasNoDependentValues()
    {
        // Arrange / Act
        var policy = RegistrationPolicy.Disabled();

        // Assert
        policy.AllowAutoRegistration.Should().BeFalse();
        policy.RequireEmployeeRecord.Should().BeNull();
        policy.DefaultRoleId.Should().BeNull();
    }

    [Fact]
    public void Enabled_ExposesDependentValues()
    {
        // Arrange / Act
        var policy = RegistrationPolicy.Enabled(requireEmployeeRecord: false, defaultRoleId: "role-1");

        // Assert
        policy.AllowAutoRegistration.Should().BeTrue();
        policy.RequireEmployeeRecord.Should().BeFalse();
        policy.DefaultRoleId.Should().Be("role-1");
    }

    [Fact]
    public void Enabled_TrimsRoleId()
    {
        // Arrange / Act — trimming is cosmetic input cleaning, still allowed.
        var policy = RegistrationPolicy.Enabled(requireEmployeeRecord: true, defaultRoleId: "  role-2  ");

        // Assert
        policy.DefaultRoleId.Should().Be("role-2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Enabled_RejectsMissingRole(string? defaultRoleId)
    {
        // Arrange / Act — the default role is required when enabled; a blank or
        // missing id is a caller error, not something to coerce to null.
        var act = () => RegistrationPolicy.Enabled(requireEmployeeRecord: true, defaultRoleId: defaultRoleId!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromFlat_WithAutoRegistrationOff_DiscardsDependentInputs()
    {
        // Arrange / Act — even though loosened inputs are supplied, a disabled
        // policy must not surface them.
        var policy = RegistrationPolicy.FromFlat(
            allowAutoRegistration: false,
            requireEmployeeRecord: false,
            defaultRoleId: "role-3");

        // Assert
        policy.AllowAutoRegistration.Should().BeFalse();
        policy.RequireEmployeeRecord.Should().BeNull();
        policy.DefaultRoleId.Should().BeNull();
    }

    [Fact]
    public void FromFlat_WithAutoRegistrationOn_KeepsDependentInputs()
    {
        // Arrange / Act
        var policy = RegistrationPolicy.FromFlat(
            allowAutoRegistration: true,
            requireEmployeeRecord: false,
            defaultRoleId: "role-4");

        // Assert
        policy.AllowAutoRegistration.Should().BeTrue();
        policy.RequireEmployeeRecord.Should().BeFalse();
        policy.DefaultRoleId.Should().Be("role-4");
    }
}
