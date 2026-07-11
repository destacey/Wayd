using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Models;
using NodaTime;

namespace Wayd.Organization.Application.TeamsOfTeams.Commands;

public sealed record CreateTeamOfTeamsCommand(string Name, TeamCode Code, string? Description, LocalDate ActiveDate) : ICommand<ObjectIdAndKey>;

public sealed class CreateTeamOfTeamsCommandValidator : CustomValidator<CreateTeamOfTeamsCommand>
{
    private readonly IOrganizationDbContext _organizationDbContext;

    public CreateTeamOfTeamsCommandValidator(IOrganizationDbContext organizationDbContext)
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

internal sealed class CreateTeamOfTeamsCommandHandler : ICommandHandler<CreateTeamOfTeamsCommand, ObjectIdAndKey>
{
    private const string RequestName = nameof(CreateTeamOfTeamsCommand);

    private readonly IOrganizationDbContext _organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateTeamOfTeamsCommandHandler> _logger;

    public CreateTeamOfTeamsCommandHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateTeamOfTeamsCommandHandler> logger)
    {
        _organizationDbContext = organizationDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<ObjectIdAndKey>> Handle(CreateTeamOfTeamsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var team = TeamOfTeams.Create(request.Name, request.Code, request.Description, request.ActiveDate, _dateTimeProvider.Now);
            await _organizationDbContext.TeamOfTeams.AddAsync(team, cancellationToken);

            await _organizationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("{RequestName}: created Team of Teams with Id {TeamId}, Key {TeamKey}, and Code {TeamCode}", RequestName, team.Id, team.Key, team.Code);

            // Sync the new team with the graph database
            // TODO: move to more of an event based approach
            await _organizationDbContext.UpsertTeamNode(TeamNode.From(team), cancellationToken);

            _logger.LogDebug("{RequestName}: synced TeamNode for Team of Teams with Id {TeamId}, Key {TeamKey}, and Code {TeamCode}", RequestName, team.Id, team.Key, team.Code);

            return Result.Success(new ObjectIdAndKey(team.Id, team.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}: {@Request}", RequestName, request);

            return Result.Failure<ObjectIdAndKey>($"Exception for request {RequestName} {request}");
        }
    }
}
