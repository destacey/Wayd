/**
 * Pure helpers for translating a drag-and-drop reorder of the portfolio ranking board into a
 * MoveProjectRanks request.
 *
 * The board shows projects in their visual order; only some carry a stored rank (others are
 * unranked and sort at the tail). The server computes the actual fractional rank values — but it can
 * only position relative to RANKED anchors. So the UI's job is to express the move as:
 *   - an ORDERED batch of project ids to (re)place, and
 *   - the two ranked anchors immediately above (`afterProjectId`) and below (`beforeProjectId`) the
 *     batch's new location.
 *
 * When a drop lands in a region whose neighbours are unranked, we walk up to the nearest ranked row
 * to use as the anchor and fold every unranked row between it and the drop point into the batch — so
 * the order the user just created survives the post-mutation refetch (otherwise still-null rows would
 * snap back to the name-ordered tail).
 */

export interface RankableRow {
  id: string
  /** The stored fractional rank sort key, or null/undefined when unranked. */
  rank?: number | null
}

export interface MoveRanksPayload {
  projectIds: string[]
  afterProjectId?: string
  beforeProjectId?: string
}

const isRanked = (row: RankableRow): boolean =>
  row.rank !== null && row.rank !== undefined

/**
 * Build the MoveRanks payload from the board's order AFTER the drag, given the indices the moved
 * selection now occupies (contiguous, ascending). Returns null when the move can't be expressed
 * (empty selection, or — defensively — no ranked anchor to position against).
 *
 * @param orderedRows  the full board in its new visual order
 * @param movedIndices the indices (into orderedRows) the dragged selection now occupies
 */
export function buildMoveRanksPayload(
  orderedRows: RankableRow[],
  movedIndices: number[],
): MoveRanksPayload | null {
  if (movedIndices.length === 0) return null
  if (!orderedRows.some(isRanked)) return null

  const sorted = [...movedIndices].sort((a, b) => a - b)
  const firstMoved = sorted[0]
  const lastMoved = sorted[sorted.length - 1]
  const movedIdSet = new Set(sorted.map((i) => orderedRows[i].id))

  // Walk up from the first moved row to the nearest ranked row → the 'after' (upper) anchor.
  let afterIndex = -1
  for (let i = firstMoved - 1; i >= 0; i--) {
    if (isRanked(orderedRows[i]) && !movedIdSet.has(orderedRows[i].id)) {
      afterIndex = i
      break
    }
  }

  // Walk down from the last moved row to the nearest ranked row → the 'before' (lower) anchor.
  let beforeIndex = -1
  for (let i = lastMoved + 1; i < orderedRows.length; i++) {
    if (isRanked(orderedRows[i]) && !movedIdSet.has(orderedRows[i].id)) {
      beforeIndex = i
      break
    }
  }

  // The batch is the moved selection plus every UNRANKED row between the upper anchor and the moved
  // block — those need concrete ranks too, or they'd re-sort to the tail on refresh. Rows below the
  // block that are unranked are left alone (they stay at the tail consistently).
  const batchStart = afterIndex === -1 ? 0 : afterIndex + 1
  const batchEnd = lastMoved

  const projectIds: string[] = []
  for (let i = batchStart; i <= batchEnd; i++) {
    const row = orderedRows[i]
    if (movedIdSet.has(row.id) || !isRanked(row)) {
      projectIds.push(row.id)
    }
  }

  return {
    projectIds,
    afterProjectId: afterIndex === -1 ? undefined : orderedRows[afterIndex].id,
    beforeProjectId: beforeIndex === -1 ? undefined : orderedRows[beforeIndex].id,
  }
}
