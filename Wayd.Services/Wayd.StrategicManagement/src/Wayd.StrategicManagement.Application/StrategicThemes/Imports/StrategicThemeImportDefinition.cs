using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Imports;

/// <summary>
/// Imports strategic themes.
/// </summary>
/// <remarks>
/// Atomic, matching the single save the command it replaces did. Themes are the natural-key anchor the
/// program and project imports resolve against by name, so a half-applied file leaves those imports
/// silently resolving some names and rejecting others.
/// </remarks>
public sealed class StrategicThemeImportDefinition(
    IStrategicManagementDbContext strategicManagementDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportStrategicThemeDto>(serializer)
{
    public const string ImportKey = "strategic.themes";

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;

    public override string Key => ImportKey;
    public override string DisplayName => "Strategic Themes";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.StrategicThemes;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportStrategicThemeDto>> Steps =>
    [
        new("CreateThemes", ImportPassScope.Chunked, CreateThemes),
    ];

    /// <summary>
    /// Rejects a row whose name is already taken, then creates what is left.
    /// </summary>
    /// <remarks>
    /// Name is the key other imports resolve against, so a reused one would attach later rows to the wrong
    /// theme. Uniqueness <em>within</em> the file is the submission command's rule, since it is a property
    /// of the file rather than of any one row.
    /// </remarks>
    private async Task<Result> CreateThemes(ImportPassContext<ImportStrategicThemeDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person edited every row by
        // hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        var names = context.Rows.Select(r => Normalize(r.Data.Name)).ToList();

        var takenNames = (await _strategicManagementDbContext.StrategicThemes
                .AsNoTracking()
                .Where(t => names.Contains(t.Name))
                .Select(t => t.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in context.Accepted)
        {
            var name = Normalize(row.Data.Name);

            if (takenNames.Contains(name))
            {
                row.Failed($"A strategic theme already exists named '{name}'.");
                continue;
            }

            // Create takes the state directly, so an archived theme needs no activate/archive transition.
            var theme = StrategicTheme.Create(
                name, row.Data.Description.Trim(), row.Data.State, actor, timestamp);

            await _strategicManagementDbContext.StrategicThemes.AddAsync(theme, cancellationToken);
            row.Created(theme.Id);
        }

        return Result.Success();
    }

    private static string Normalize(string name) => name.Trim();
}
