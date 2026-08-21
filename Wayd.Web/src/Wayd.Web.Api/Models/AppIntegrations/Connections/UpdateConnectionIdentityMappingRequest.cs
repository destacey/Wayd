using Wayd.AppIntegration.Application.Connections.Commands.Identities;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

/// <summary>
/// One admin decision about one external identity on a connection.
/// </summary>
public sealed record UpdateConnectionIdentityMappingRequest
{
    /// <summary>The connection the identity belongs to.</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>The identity mapping being decided.</summary>
    public Guid MappingId { get; set; }

    /// <summary>Map, Ignore, or Clear.</summary>
    public IdentityMappingAction Action { get; set; }

    /// <summary>The employee to attribute this identity to. Required when the action is Map.</summary>
    public Guid? EmployeeId { get; set; }

    public UpdateConnectionIdentityMappingCommand ToUpdateConnectionIdentityMappingCommand(Guid[] validEmployeeIds) =>
        new(ConnectionId, MappingId, Action, EmployeeId, validEmployeeIds);
}

public sealed class UpdateConnectionIdentityMappingRequestValidator : CustomValidator<UpdateConnectionIdentityMappingRequest>
{
    public UpdateConnectionIdentityMappingRequestValidator()
    {
        RuleFor(r => r.ConnectionId)
            .NotEmpty();

        RuleFor(r => r.MappingId)
            .NotEmpty();

        RuleFor(r => r.Action)
            .IsInEnum();

        RuleFor(r => r.EmployeeId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .When(r => r.Action == IdentityMappingAction.Map)
            .WithMessage("An employee is required when mapping an identity.");
    }
}
