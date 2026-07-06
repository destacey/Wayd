import { arrayMove } from '@dnd-kit/sortable'
import type { Column, ColumnOrderState, Table } from '@tanstack/react-table'

/**
 * Column-ordering helpers for WaydGrid. Pure and table-agnostic so they're
 * unit-testable without a table instance.
 *
 * Background — how TanStack (v8) composes columnOrder with pinning:
 *  - `columnOrder` is a list of leaf column ids. TanStack orders the columns
 *    by it, then APPENDS any leaf id missing from the list at the end (not at
 *    its def position).
 *  - The rendered order is left-pinned → center → right-pinned. Only the
 *    CENTER section follows `columnOrder`; each pinned section is ordered by
 *    its `columnPinning` array, which ignores `columnOrder` entirely.
 *
 * So the grid keeps `columnOrder` as the FULL leaf-id order (pinned ids
 * included but inert for pinned sections) and reorders within it for center
 * drags; pinned-section drags reorder the relevant `columnPinning` array.
 */

/**
 * Reconciles a persisted (or otherwise stale) column order against the current
 * leaf columns so it's safe to hand to TanStack:
 *  - keeps stored ids that still exist, in their stored order,
 *  - inserts each current id absent from the stored order at its DEF index
 *    (so a newly added column appears where its def author placed it, rather
 *    than being appended to the end as TanStack would do),
 *  - drops stored ids for columns that no longer exist.
 *
 * An empty stored order returns the def order (identity), which the grid then
 * treats as "no custom order".
 *
 * @param storedOrder  the persisted order (may reference removed columns / omit new ones)
 * @param currentLeafIds  the current leaf column ids in DEF order
 */
export function reconcileColumnOrder(
  storedOrder: ColumnOrderState,
  currentLeafIds: string[],
): ColumnOrderState {
  const currentSet = new Set(currentLeafIds)
  // Known stored ids, in stored order (removed ids dropped).
  const kept = storedOrder.filter((id) => currentSet.has(id))
  const keptSet = new Set(kept)

  const defIndexOf = new Map(currentLeafIds.map((id, i) => [id, i]))

  // Walk the def order; splice each not-yet-placed id in just AFTER the most
  // recent already-placed id that is def-earlier than it. Anchoring on the
  // def-earlier neighbour (rather than the first def-later one) respects a
  // user's reordering: a new column follows its def predecessor wherever the
  // user moved that predecessor to.
  const result: string[] = [...kept]
  currentLeafIds.forEach((id, defIndex) => {
    if (keptSet.has(id)) return
    let insertAt = 0
    for (let i = 0; i < result.length; i++) {
      if ((defIndexOf.get(result[i]) ?? -1) < defIndex) {
        insertAt = i + 1
      }
    }
    result.splice(insertAt, 0, id)
    keptSet.add(id)
  })

  return result
}

/**
 * Moves `activeId` to `overId`'s position within a list of ids (a full
 * columnOrder or a single columnPinning side). No-op — returns the same
 * reference — when either id is absent or they're identical, so callers can
 * safely bail on a non-move.
 */
export function reorderIds(
  ids: string[],
  activeId: string,
  overId: string,
): string[] {
  if (activeId === overId) return ids
  const from = ids.indexOf(activeId)
  const to = ids.indexOf(overId)
  if (from < 0 || to < 0) return ids
  return arrayMove(ids, from, to)
}

/**
 * The visible leaf columns in RENDER order: left-pinned → center → right-pinned
 * (the center honours columnOrder). Everything that iterates columns for the
 * user — the colgroup, CSV export, the column chooser, autosize-all — must use
 * this, because plain getVisibleLeafColumns() stays in def order and ignores
 * both pinning and columnOrder.
 */
export function getOrderedVisibleLeafColumns<T>(
  table: Table<T>,
): Column<T, unknown>[] {
  // The pin-section getters read table.getState().columnPinning; a headless
  // harness may omit it. columnOrder is still applied by getVisibleLeafColumns,
  // so fall back to that (def-position for pinned, but nothing is pinned when
  // there's no pinning state anyway).
  if (!table.getState().columnPinning) return table.getVisibleLeafColumns()
  return [
    ...table.getLeftVisibleLeafColumns(),
    ...table.getCenterVisibleLeafColumns(),
    ...table.getRightVisibleLeafColumns(),
  ]
}

/**
 * ALL leaf columns (visible AND hidden) in the on-screen left→center→right
 * order: left-pinned first (pin-array order), then the unpinned columns in
 * columnOrder sequence, then right-pinned. Like {@link
 * getOrderedVisibleLeafColumns} but keeps hidden columns, so the Choose
 * Columns list mirrors the display order while still listing hideable columns
 * the user has turned off.
 */
export function getOrderedAllLeafColumns<T>(
  table: Table<T>,
): Column<T, unknown>[] {
  const { left = [], right = [] } = table.getState().columnPinning ?? {}
  const pinned = new Set([...left, ...right])
  // getAllLeafColumns() already applies columnOrder to every leaf.
  const byId = new Map(
    table.getAllLeafColumns().map((column) => [column.id, column]),
  )
  const lookup = (id: string) => byId.get(id)
  const leftCols = left.map(lookup).filter(Boolean) as Column<T, unknown>[]
  const rightCols = right.map(lookup).filter(Boolean) as Column<T, unknown>[]
  const centerCols = table
    .getAllLeafColumns()
    .filter((column) => !pinned.has(column.id))
  return [...leftCols, ...centerCols, ...rightCols]
}
