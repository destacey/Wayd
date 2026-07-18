using CSharpFunctionalExtensions;
using FluentValidation;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Validation;
using Wayd.Links.Models;

namespace Wayd.Links.Commands;

public sealed record UpdateLinkCommand(Guid LinkId, string Name, string Url) : ICommand<LinkDto>;

public sealed class UpdateLinkCommandValidator : CustomValidator<UpdateLinkCommand>
{
    public UpdateLinkCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(l => l.LinkId)
            .NotEmpty();

        RuleFor(l => l.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(l => l.Url)
            .NotEmpty();
    }
}

public sealed class UpdateLinkCommandHandler : ICommandHandler<UpdateLinkCommand, LinkDto>
{
    private readonly ILinksDbContext _linksDbContext;
    private readonly ILogger<UpdateLinkCommandHandler> _logger;

    public UpdateLinkCommandHandler(ILinksDbContext linksDbContext, ILogger<UpdateLinkCommandHandler> logger)
    {
        _linksDbContext = linksDbContext;
        _logger = logger;
    }

    public async Task<Result<LinkDto>> Handle(UpdateLinkCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var link = await _linksDbContext.Links
                .FirstOrDefaultAsync(l => l.Id == request.LinkId, cancellationToken);
            if (link is null)
            {
                _logger.LogError("Unable to update link. Link with id {LinkId} not found.", request.LinkId);
                return Result.Failure<LinkDto>($"Link with id {request.LinkId} not found.");
            }

            var updateResult = link.Update(request.Name, request.Url);
            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _linksDbContext.Entry(link).ReloadAsync(cancellationToken);
                link.ClearDomainEvents();

                var requestName = request.GetType().Name;
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", requestName, request, updateResult.Error);
                return Result.Failure<LinkDto>(updateResult.Error);
            }

            await _linksDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(link.Adapt<LinkDto>());
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<LinkDto>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
