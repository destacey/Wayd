using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Persistence;

/// <summary>
/// The import runner's own tables.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWaydDbContext"/> for the same reason as <see cref="IStatusWorkflowDbContext"/>:
/// every module's import handlers write row outcomes, so each module's test fake has to supply these tables
/// — and hanging them off the global interface would make that fake owe an implementation of the whole
/// thing, employees and providers and scoring models included, none of which an import handler touches.
/// </remarks>
public interface IImportDbContext
{
    DbSet<ImportProcess> ImportProcesses { get; }
    DbSet<ImportProcessRow> ImportProcessRows { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
