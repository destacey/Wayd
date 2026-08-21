using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Validators;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// 
/// </summary>
/// <param name="ConnectionId">The connection whose sync produced these items. Scopes external identity mappings.</param>
/// <param name="Connector">The external system these items came from.</param>
/// <param name="WorkspaceId"></param>
/// <param name="WorkItems"></param>
/// <param name="TeamMappings">The key is set to the external team id and value is set to the internal (Wayd) team id.</param>
/// <param name="IterationMappings">The key is set to the external iteration id and value is set to the internal (Wayd) iteration id.</param>
public sealed record SyncExternalWorkItemsCommand(Guid ConnectionId, Connector Connector, Guid WorkspaceId, List<IExternalWorkItem> WorkItems, Dictionary<Guid, Guid?> TeamMappings, Dictionary<string, Guid> IterationMappings) : ICommand, ILongRunningRequest;

public sealed class SyncExternalWorkItemsCommandValidator : CustomValidator<SyncExternalWorkItemsCommand>
{
    public SyncExternalWorkItemsCommandValidator()
    {
        RuleFor(c => c.ConnectionId)
            .NotEmpty();

        RuleFor(c => c.WorkspaceId)
            .NotEmpty();

        RuleForEach(c => c.WorkItems)
            .NotNull()
            .SetValidator(new IExternalWorkItemValidator());

        RuleFor(c => c.TeamMappings)
            .NotNull();

        When(c => c.TeamMappings.Count > 0, () =>
        {
            RuleForEach(c => c.TeamMappings).ChildRules(teamMapping =>
            {
                teamMapping.RuleFor(tm => tm.Key)
                    .NotEmpty();

                teamMapping.When(tm => tm.Value.HasValue, () =>
                {
                    teamMapping.RuleFor(tm => tm.Value)
                        .NotEmpty()
                        .Must(v => v.HasValue && v.Value != Guid.Empty);
                });
            });
        });

        RuleFor(c => c.IterationMappings)
            .NotNull();

        When(c => c.IterationMappings.Count > 0, () =>
        {
            RuleForEach(c => c.IterationMappings).ChildRules(iterationMapping =>
            {
                iterationMapping.RuleFor(tm => tm.Key)
                    .NotEmpty();

                iterationMapping.RuleFor(tm => tm.Value)
                    .NotEmpty();
            });
        });
    }
}
