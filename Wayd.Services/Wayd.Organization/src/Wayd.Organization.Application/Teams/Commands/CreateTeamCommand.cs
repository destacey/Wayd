using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Models;
using Wayd.Organization.Domain.Enums;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Commands;

public sealed record CreateTeamCommand(string Name, TeamCode Code, string? Description, LocalDate ActiveDate) : ICommand<ObjectIdAndKey>;

public sealed class CreateTeamCommandValidator : CustomValidator<CreateTeamCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext;

    public CreateTeamCommandValidator(IOrganizationDbContext organizationDbContext)
    {
        _organizationDbContext = organizationDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(BeUniqueTeamName).WithMessage("The Team name already exists.");

        RuleFor(t => t.Code)
            .NotEmpty()
            .SetValidator(new TeamCodeValidator(_organizationDbContext));

        RuleFor(t => t.Description)
            .MaximumLength(1024);

        RuleFor(t => t.ActiveDate)
            .NotEmpty();
    }

    public async Task<bool> BeUniqueTeamName(string name, CancellationToken cancellationToken)
    {
        return await _organizationDbContext.BaseTeams.AllAsync(x => x.Name != name, cancellationToken);
    }
}

internal sealed class CreateTeamCommandHandler : ICommandHandler<CreateTeamCommand, ObjectIdAndKey>
{
    private const string RequestName = nameof(CreateTeamCommand);

    private readonly IOrganizationDbContext _organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateTeamCommandHandler> _logger;

    public CreateTeamCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateTeamCommandHandler> logger)
    {
        _organizationDbContext = organizationDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<ObjectIdAndKey>> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Create team with default operating model (Kanban + Count)
            var team = Team.Create(
                request.Name,
                request.Code,
                request.Description,
                request.ActiveDate,
                Methodology.Kanban,
                SizingMethod.Count,
                _dateTimeProvider.Now);

            await _organizationDbContext.Teams.AddAsync(team, cancellationToken);
            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("{RequestName}: created Team with Id {TeamId}, Key {TeamKey}, and Code {TeamCode}", RequestName, team.Id, team.Key, team.Code.Value);

            // Sync the new team with the graph database
            // TODO: move to more of an event based approach
            await _organizationDbContext.UpsertTeamNode(TeamNode.From(team), cancellationToken);

            _logger.LogDebug("{RequestName}: synced TeamNode for Team with Id {TeamId}, Key {TeamKey}, and Code {TeamCode}", RequestName, team.Id, team.Key, team.Code.Value);

            return Result.Success(new ObjectIdAndKey(team.Id, team.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);

            return Result.Failure<ObjectIdAndKey>($"Exception for request {RequestName} {request}");
        }
    }
}
