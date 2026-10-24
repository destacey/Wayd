/**
 * The nearest ancestor that scrolls vertically, or null when only the document does.
 *
 * The app shell scrolls its content area, not the window, so anything that
 * tracks scrolling must find the element that actually moves.
 */
export const findScrollParent = (element: HTMLElement): HTMLElement | null => {
  let current = element.parentElement
  while (current) {
    const { overflowY } = getComputedStyle(current)
    if (overflowY === 'auto' || overflowY === 'scroll') {
      return current
    }
    current = current.parentElement
  }
  return null
}
