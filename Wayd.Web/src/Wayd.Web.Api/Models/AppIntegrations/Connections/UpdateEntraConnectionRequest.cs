using Wayd.AppIntegration.Application.Connections.Commands.Entra;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record UpdateEntraConnectionRequest : UpdateConnectionRequest
{
    /// <summary>
    /// The Entra ID (Azure AD) tenant identifier.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// The application (client) identifier for the Entra app registration used to call Microsoft Graph.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// The client secret for the Entra app registration.
    /// </summary>
    public required string ClientSecret { get; set; }

    /// <summary>
    /// Optional Entra group object ID to scope the user query to. When null, all member users in
    /// the tenant are queried.
    /// </summary>
    public string? AllUsersGroupObjectId { get; set; }

    /// <summary>
    /// When true, users with disabled accounts are also included in the sync.
    /// </summary>
    public bool IncludeDisabledUsers { get; set; }

    /// <summary>
    /// Which uniquely-indexed Employee field the sync upsert matches on.
    /// </summary>
    public EmployeeMatchProperty MatchBy { get; set; } = EmployeeMatchProperty.Email;

    public UpdateEntraConnectionCommand ToCommand()
        => new(Id, Name, Description, TenantId, ClientId, ClientSecret, AllUsersGroupObjectId, IncludeDisabledUsers, MatchBy);
}

public sealed class UpdateEntraConnectionRequestValidator : CustomValidator<UpdateEntraConnectionRequest>
{
    public UpdateEntraConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new UpdateConnectionRequestValidator());

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.ClientId)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.ClientSecret)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.AllUsersGroupObjectId)
            .MaximumLength(64);
    }
}
