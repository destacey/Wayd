using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Employees;

namespace Wayd.AppIntegration.Application.Connections.Commands.Identities;

/// <summary>
/// What an admin decided about one external identity.
/// </summary>
public enum IdentityMappingAction
{
    /// <summary>Point the identity at an employee.</summary>
    Map = 0,

    /// <summary>Mark the identity as one that will never have an employee.</summary>
    Ignore = 1,

    /// <summary>Undo a prior decision, returning the identity to the review queue.</summary>
    Clear = 2,
}

/// <summary>
/// Applies one admin decision to one external identity.
/// </summary>
/// <remarks>
/// Deliberately per-row rather than the whole-list replace the team mappings use. A connection can
/// carry hundreds of identities, and posting every row to change one invites lost updates when two
/// admins work the queue at once.
/// </remarks>
/// <param name="ValidEmployeeIds">
/// Resolved by the caller so the handler never trusts an employee id straight off the wire, matching
/// how team mappings validate.
/// </param>
public sealed record UpdateConnectionIdentityMappingCommand(
    Guid ConnectionId,
    Guid MappingId,
    IdentityMappingAction Action,
    Guid? EmployeeId,
    Guid[] ValidEmployeeIds) : ICommand;

public sealed class UpdateConnectionIdentityMappingCommandValidator : CustomValidator<UpdateConnectionIdentityMappingCommand>
{
    public UpdateConnectionIdentityMappingCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.ConnectionId)
            .NotEmpty();

        RuleFor(c => c.MappingId)
            .NotEmpty();

        RuleFor(c => c.Action)
            .IsInEnum();

        RuleFor(c => c.EmployeeId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .When(c => c.Action == IdentityMappingAction.Map)
            .WithMessage("An employee is required when mapping an identity.");
    }
}

public sealed class UpdateConnectionIdentityMappingCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IDispatcher dispatcher,
    ILogger<UpdateConnectionIdentityMappingCommandHandler> logger)
    : ICommandHandler<UpdateConnectionIdentityMappingCommand>
{
    private const string AppRequestName = nameof(UpdateConnectionIdentityMappingCommand);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<UpdateConnectionIdentityMappingCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateConnectionIdentityMappingCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Scoped by connection as well as id: a mapping id from another connection must not be
            // reachable by guessing it through this connection's endpoint.
            var mapping = await _appIntegrationDbContext.ExternalIdentityMappings
                .FirstOrDefaultAsync(m => m.Id == request.MappingId && m.ConnectionId == request.ConnectionId, cancellationToken);
            if (mapping is null)
                return Result.Failure("External identity mapping not found.");

            switch (request.Action)
            {
                case IdentityMappingAction.Map:
                    if (!request.EmployeeId.HasValue)
                        return Result.Failure("An employee is required when mapping an identity.");

                    if (!request.ValidEmployeeIds.Contains(request.EmployeeId.Value))
                    {
                        _logger.LogWarning("{AppRequestName}: Invalid employee {EmployeeId} for connection {ConnectionId} mapping {MappingId}.",
                            AppRequestName, request.EmployeeId, request.ConnectionId, request.MappingId);
                        return Result.Failure("The selected employee could not be found.");
                    }

                    var mapResult = mapping.MapToEmployee(request.EmployeeId.Value);
                    if (mapResult.IsFailure)
                        return mapResult;
                    break;

                case IdentityMappingAction.Ignore:
                    mapping.Ignore();
                    break;

                case IdentityMappingAction.Clear:
                    mapping.ClearDecision();
                    break;

                default:
                    return Result.Failure($"Unsupported identity mapping action '{request.Action}'.");
            }

            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{AppRequestName}: Connection {ConnectionId} identity {MappingId} set to {Status}.",
                AppRequestName, request.ConnectionId, request.MappingId, mapping.Status);

            // Carry the decision back to work already synced. Ignoring an identity clears the
            // attribution rather than leaving a wrong one standing: an admin saying "this is
            // nobody" is a stronger statement than the auto-match that put a name there.
            //
            // Dispatched after the commit and handled asynchronously — one identity can own tens
            // of thousands of work items, and an admin picking a name from a dropdown should not
            // wait on that.
            await _dispatcher.Send(
                new RepointWorkItemAttributionCommand(mapping.ExternalId, mapping.EmployeeId),
                cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{AppRequestName}: Error updating connection {ConnectionId} identity {MappingId}.",
                AppRequestName, request.ConnectionId, request.MappingId);
            return Result.Failure(ex.Message);
        }
    }
}
