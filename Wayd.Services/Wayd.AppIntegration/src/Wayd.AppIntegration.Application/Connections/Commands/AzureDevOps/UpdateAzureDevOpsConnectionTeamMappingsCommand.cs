using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;

namespace Wayd.AppIntegration.Application.Connections.Commands;

public sealed record UpdateAzureDevOpsConnectionTeamMappingsCommand(Guid ConnectionId, List<AzureDevOpsWorkspaceTeamMappingDto> TeamMappings, Guid[] ValidTeamIds) : ICommand;

public sealed class AzdoConnectionTeamMappingsRequestValidator : CustomValidator<UpdateAzureDevOpsConnectionTeamMappingsCommand>
{
    public AzdoConnectionTeamMappingsRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.ConnectionId)
            .NotEmpty();

        RuleForEach(t => t.TeamMappings)
            .NotNull()
            .SetValidator(new AzureDevOpsWorkspaceTeamMappingDtoValidator());
    }
}

public sealed class UpdateAzureDevOpsConnectionTeamMappingsCommandHandler : ICommandHandler<UpdateAzureDevOpsConnectionTeamMappingsCommand>
{
    private const string AppRequestName = nameof(UpdateAzureDevOpsConnectionTeamMappingsCommand);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext;
    private readonly ILogger<UpdateAzureDevOpsConnectionTeamMappingsCommandHandler> _logger;

    public UpdateAzureDevOpsConnectionTeamMappingsCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ILogger<UpdateAzureDevOpsConnectionTeamMappingsCommandHandler> logger)
    {
        _appIntegrationDbContext = appIntegrationDbContext;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateAzureDevOpsConnectionTeamMappingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
                .FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);
            if (connection is null)
                return Result.Failure("Azure DevOps connection not found.");


            var teamConfig = connection.TeamConfiguration;
            foreach (var team in request.TeamMappings)
            {
                if (team.InternalTeamId.HasValue && !request.ValidTeamIds.Contains(team.InternalTeamId.Value))
                {
                    _logger.LogError("{AppRequestName}: Invalid internal team {TeamId} for connection {ConnectionId} workspace {WorkspaceId} and team {ExternalTeamId}.", AppRequestName, team.InternalTeamId, request.ConnectionId, team.WorkspaceId, team.ExternalTeamId);
                    continue;
                }

                var existingTeam = teamConfig.WorkspaceTeams.SingleOrDefault(w => w.WorkspaceId == team.WorkspaceId && w.TeamId == team.ExternalTeamId);
                if (existingTeam is not null)
                {
                    if (existingTeam.InternalTeamId != team.InternalTeamId)
                    {
                        if (team.InternalTeamId is null)
                        {
                            // TODO: this is a hack because EF/SQL is throwing an exception when we set the InternalTeamId to null.  This seems to happen
                            // because the stored procedure paramater value is NULL.  It works when the paramater is bypassed.
                            var resetTeamResult = teamConfig.ResetTeam(team.ExternalTeamId);
                            if (resetTeamResult.IsFailure)
                                return resetTeamResult;
                        }
                        else
                        {
                            existingTeam.MapInternalTeam(team.InternalTeamId);
                        }
                        _logger.LogDebug("{AppRequestName}: Team mapping removed for connection {ConnectionId} workspace {WorkspaceId}. External team {ExternalTeamId}.", AppRequestName, request.ConnectionId, team.WorkspaceId, team.ExternalTeamId);
                    }
                    else
                    {
                        // reset the InternalTeamId if it is no longer valid.
                        if (existingTeam.InternalTeamId.HasValue && !request.ValidTeamIds.Contains(existingTeam.InternalTeamId.Value))
                        {
                            _logger.LogError("{AppRequestName}: Invalid current internal team {TeamId} for connection {ConnectionId} workspace {WorkspaceId} and team {ExternalTeamId}.", AppRequestName, existingTeam.InternalTeamId, request.ConnectionId, team.WorkspaceId, team.ExternalTeamId);

                            // TODO: this is a hack because EF/SQL is throwing an exception when we set the InternalTeamId to null.  This seems to happen
                            // because the stored procedure paramater value is NULL.  It works when the paramater is bypassed.
                            var resetTeamResult = teamConfig.ResetTeam(team.ExternalTeamId);
                            if (resetTeamResult.IsFailure)
                                return resetTeamResult;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("{AppRequestName}: Team mapping not found for connection {ConnectionId} workspace {WorkspaceId} and team {ExternalTeamId}.", AppRequestName, request.ConnectionId, team.WorkspaceId, team.ExternalTeamId);
                }
            }

            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{AppRequestName}: Connection {ConnectionId} team mappings updated.", AppRequestName, request.ConnectionId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{AppRequestName}: Error updating connection {ConnectionId} team mappings.", AppRequestName, request.ConnectionId);
            return Result.Failure(ex.Message);
        }
    }
}

