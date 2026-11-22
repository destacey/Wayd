using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTypes.Commands;

public sealed record CreateWorkTypeCommand(string Name, string? Description, int LevelId) : ICommand<int>;

public sealed class CreateWorkTypeCommandValidator : CustomValidator<CreateWorkTypeCommand>
{
    private readonly IWorkDbContext _workDbContext;

    public CreateWorkTypeCommandValidator(IWorkDbContext workDbContext)
    {
        _workDbContext = workDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(64)
            .MustAsync(BeUniqueName).WithMessage("The work type already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.LevelId)
            .GreaterThan(0);
    }

    public async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
    {
        return await _workDbContext.WorkTypes
            .AllAsync(e => e.Name != name, cancellationToken);
    }
}

public sealed class CreateWorkTypeCommandHandler : ICommandHandler<CreateWorkTypeCommand, int>
{
    private readonly IWorkDbContext _workDbContext;
    private readonly Instant _timestamp;

    public CreateWorkTypeCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider)
    {
        _workDbContext = workDbContext;
        _timestamp = dateTimeProvider.Now;
    }

    public async Task<Result<int>> Handle(CreateWorkTypeCommand request, CancellationToken cancellationToken)
    {
        var workType = WorkType.Create(request.Name, request.Description, request.LevelId, _timestamp);

        await _workDbContext.WorkTypes.AddAsync(workType, cancellationToken);

        await _workDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(workType.Id);
    }
}

