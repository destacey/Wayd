using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Validators;

namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

public sealed record SyncAzureDevOpsIterationsCommand(string SystemId, List<IExternalIteration<AzdoIterationMetadata>> Iterations, Dictionary<Guid, Guid?> TeamMappings) : ICommand, ILongRunningRequest;

public sealed class SyncAzureDevOpsIterationsCommandValidator : CustomValidator<SyncAzureDevOpsIterationsCommand>
{
    public SyncAzureDevOpsIterationsCommandValidator()
    {
        RuleFor(c => c.SystemId)
            .NotEmpty();

        RuleForEach(c => c.Iterations)
            .NotNull()
            .SetValidator(new IExternalIterationValidator<AzdoIterationMetadata>());

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
    }
}
