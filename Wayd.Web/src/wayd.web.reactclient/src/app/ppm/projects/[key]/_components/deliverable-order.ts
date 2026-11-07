/**
 * The full order to send after the reader reorders the deliverables they can
 * see. The server needs every id, so the hidden ones stay at the positions
 * they already held and the visible ones fill the remaining positions in their
 * new order.
 *
 * @param currentIds every deliverable's id, in the server's current order
 * @param hiddenIds the ids not shown in the grid
 * @param reorderedVisibleIds the shown ids, in their new order
 */
export const mergeDeliverableOrder = (
  currentIds: string[],
  hiddenIds: ReadonlySet<string>,
  reorderedVisibleIds: string[],
): string[] => {
  const visible = reorderedVisibleIds[Symbol.iterator]()
  return currentIds.map((id) =>
    hiddenIds.has(id) ? id : (visible.next().value ?? id),
  )
}
