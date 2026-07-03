import type { ColumnDef } from '@tanstack/react-table'

/**
 * Replaces a dotted `accessorKey` (e.g. `'team.name'`) with an equivalent
 * null-tolerant `accessorFn`. TanStack's own deep accessor `console.warn`s in
 * dev whenever an intermediate hop is undefined — noise for our optional
 * relations (`team?`, `sprint?`, `parent?`); the swapped-in walk returns the
 * same `undefined` silently. The column id must stay what TanStack would have
 * derived (dots → underscores) so sorting/filter state and `data-column-id`
 * hooks are unaffected. Recurses into grouped-header defs.
 */
export const applySafeAccessor = <T>(
  col: ColumnDef<T, unknown>,
): ColumnDef<T, unknown> => {
  const children = (col as { columns?: ColumnDef<T, unknown>[] }).columns
  if (children) {
    return {
      ...col,
      columns: children.map((child) => applySafeAccessor(child)),
    } as ColumnDef<T, unknown>
  }

  const accessorKey = (col as { accessorKey?: string | number }).accessorKey
  if (typeof accessorKey !== 'string' || !accessorKey.includes('.')) return col

  const path = accessorKey.split('.')
  const next = {
    ...col,
    id: col.id ?? path.join('_'),
    accessorFn: (row: T) => {
      let value: unknown = row
      for (const key of path) {
        if (value == null) return undefined
        value = (value as Record<string, unknown>)[key]
      }
      return value
    },
  } as ColumnDef<T, unknown>
  delete (next as { accessorKey?: unknown }).accessorKey
  return next
}
