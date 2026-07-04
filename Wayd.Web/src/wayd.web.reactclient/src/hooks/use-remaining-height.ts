import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Returns the remaining viewport height below the top of the referenced element,
 * along with a callback ref to attach to the target element.
 *
 * Uses a callback ref so it reacts immediately when the element mounts — even if
 * mounting is deferred behind a responsive breakpoint or async gate. Recalculates
 * automatically on window resize and when the nearest scrollable ancestor resizes
 * (which shifts the element's viewport position).
 *
 * @param bottomOffset - Optional pixel padding to subtract from the bottom (e.g., for page margins). Defaults to 30.
 * @returns A tuple of `[callbackRef, height]`.
 *
 * @example
 * ```tsx
 * const [containerRef, height] = useRemainingHeight()
 *
 * return (
 *   <div ref={containerRef} style={{ height }}>
 *     <DataGrid height={height} />
 *   </div>
 * )
 * ```
 */
export function useRemainingHeight(
  bottomOffset: number = 50,
): [ref: (node: HTMLElement | null) => void, height: number] {
  const [height, setHeight] = useState(500)
  const elementRef = useRef<HTMLElement | null>(null)
  const roRef = useRef<ResizeObserver | null>(null)

  const calculate = useCallback(() => {
    if (!elementRef.current) return
    const top = elementRef.current.getBoundingClientRect().top
    setHeight(Math.max(300, window.innerHeight - top - bottomOffset))
  }, [bottomOffset])

  // Recalculate on window resize
  useEffect(() => {
    window.addEventListener('resize', calculate, { passive: true })
    return () => window.removeEventListener('resize', calculate)
  }, [calculate])

  // Callback ref — fires when the element mounts or unmounts.
  // Must be stable so React doesn't repeatedly detach/reattach.
  const callbackRef = useCallback(
    (node: HTMLElement | null) => {
      // Clean up previous observer
      roRef.current?.disconnect()
      roRef.current = null
      elementRef.current = node

      if (!node) return

      // Calculate immediately now that the element is in the DOM
      const top = node.getBoundingClientRect().top
      setHeight(Math.max(300, window.innerHeight - top - bottomOffset))

      // Observe the nearest scrollable ancestor. When its content changes size
      // (e.g. sibling components load data and grow), this element's viewport
      // position shifts and we need to recalculate.
      const scrollParent = findScrollParent(node)
      if (scrollParent) {
        roRef.current = new ResizeObserver(calculate)
        roRef.current.observe(scrollParent)
      }
    },
    [calculate, bottomOffset],
  )

  // Disconnect observer on unmount
  useEffect(() => {
    return () => {
      roRef.current?.disconnect()
    }
  }, [])

  return [callbackRef, height]
}

/**
 * Walks up the DOM tree to find the nearest ancestor with scrollable overflow.
 */
function findScrollParent(element: HTMLElement): HTMLElement | null {
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
