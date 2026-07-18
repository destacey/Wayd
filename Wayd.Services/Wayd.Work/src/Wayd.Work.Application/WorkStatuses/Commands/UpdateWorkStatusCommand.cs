using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkStatuses.Commands;

public sealed record UpdateWorkStatusCommand : ICommand<int>
{
    public UpdateWorkStatusCommand(int id, string? description)
    {
        Id = id;
        Description = description;
    }

    public int Id { get; }

    /// <summary>The description of the work status.</summary>
    /// <value>The description.</value>
    public string? Description { get; }
}

public sealed class UpdateWorkStatusCommandValidator : CustomValidator<UpdateWorkStatusCommand>
{
    public UpdateWorkStatusCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description)
            .MaximumLength(1024);
    }
}

public sealed class UpdateWorkStatusCommandHandler : ICommandHandler<UpdateWorkStatusCommand, int>
{
    private readonly IWorkDbContext _workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdateWorkStatusCommandHandler> _logger;

    public UpdateWorkStatusCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateWorkStatusCommandHandler> logger)
    {
        _workDbContext = workDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(UpdateWorkStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _workDbContext.WorkStatuses
                .FirstAsync(p => p.Id == request.Id, cancellationToken);
            if (status is null)
                return Result.Failure<int>("Work Status not found.");

            var updateResult = status.Update(request.Description, _dateTimeProvider.Now);

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _workDbContext.Entry(status).ReloadAsync(cancellationToken);
                status.ClearDomainEvents();

                var requestName = request.GetType().Name;
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", requestName, request, updateResult.Error);
                return Result.Failure<int>(updateResult.Error);
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(status.Id);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}

