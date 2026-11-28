using FluentValidation;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Domain.Enums.AppIntegrations;
using NodaTime;

namespace Wayd.AppIntegration.Application.Connections.Commands.Entra;

public sealed record CreateEntraConnectionCommand(
    string Name,
    string? Description,
    string TenantId,
    string ClientId,
    string ClientSecret,
    string? AllUsersGroupObjectId,
    bool IncludeDisabledUsers,
    EmployeeMatchProperty MatchBy,
    bool NormalizeNameCasing) : ICommand<Guid>;

public sealed class CreateEntraConnectionCommandValidator : CustomValidator<CreateEntraConnectionCommand>
{
    public CreateEntraConnectionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.TenantId)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.ClientId)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.ClientSecret)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(c => c.AllUsersGroupObjectId)
            .MaximumLength(64);
    }
}

public sealed class CreateEntraConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IExternalSystemLinker externalSystemLinker)
    : ICommandHandler<CreateEntraConnectionCommand, Guid>
{
    private readonly IExternalSystemLinker _externalSystemLinker = externalSystemLinker;
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(CreateEntraConnectionCommand request, CancellationToken cancellationToken)
    {
        Instant timestamp = _dateTimeProvider.Now;

        var config = new EntraConnectionConfiguration(
            request.TenantId,
            request.ClientId,
            request.ClientSecret,
            request.AllUsersGroupObjectId,
            request.IncludeDisabledUsers,
            request.MatchBy,
            request.NormalizeNameCasing);

        // TODO: Test the connection here and set IsValidConfiguration based on the outcome.
        var isConfigurationValid = true;

        var actor = EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId());
        var connection = EntraConnection.Create(request.Name, request.Description, config, isConfigurationValid, actor, timestamp);

        var linkResult = await _externalSystemLinker.Link(connection, actor, timestamp, cancellationToken);
        if (linkResult.IsFailure)
            return Result.Failure<Guid>(linkResult.Error);

        await _appIntegrationDbContext.EntraConnections.AddAsync(connection, cancellationToken);

        var saveResult = await _externalSystemLinker.Save(cancellationToken);
        if (saveResult.IsFailure)
            return Result.Failure<Guid>(saveResult.Error);

        return Result.Success(connection.Id);
    }
}
