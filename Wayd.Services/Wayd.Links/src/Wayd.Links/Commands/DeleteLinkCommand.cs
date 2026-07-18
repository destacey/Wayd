using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;

namespace Wayd.Links.Commands;

public sealed record DeleteLinkCommand(Guid LinkId) : ICommand;

public sealed class DeleteLinkCommandHandler : ICommandHandler<DeleteLinkCommand>
{
    private readonly ILinksDbContext _linksDbContext;
    private readonly ILogger<DeleteLinkCommandHandler> _logger;

    public DeleteLinkCommandHandler(ILinksDbContext linksDbContext, ILogger<DeleteLinkCommandHandler> logger)
    {
        _linksDbContext = linksDbContext;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteLinkCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var link = await _linksDbContext.Links
                .FirstOrDefaultAsync(l => l.Id == request.LinkId, cancellationToken);
            if (link is null)
            {
                _logger.LogError("Unable to delete link. Link with id {LinkId} not found.", request.LinkId);
                return Result.Failure($"Link with id {request.LinkId} not found.");
            }

            _linksDbContext.Links.Remove(link);

            await _linksDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
