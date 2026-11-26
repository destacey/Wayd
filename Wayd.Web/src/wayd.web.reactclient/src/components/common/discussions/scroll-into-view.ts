/**
 * Scrolls an element into view, smoothly unless the reader asked for reduced
 * motion. `matchMedia` is missing in some test environments, which reads as no
 * preference.
 */
export const scrollIntoViewGently = (
  element: Element | null,
  block: ScrollLogicalPosition = 'start',
) => {
  const reduce =
    typeof window !== 'undefined' &&
    window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches
  element?.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block })
}
