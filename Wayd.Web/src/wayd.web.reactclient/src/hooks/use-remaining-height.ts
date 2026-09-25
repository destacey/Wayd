'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Returns the remaining viewport height below the top of the referenced element,
 * along with a callback ref to attach to the target element.
 *
 * Uses a callback ref so it reacts immediately when the element mounts — even if
 * mounting is deferred behind a responsive breakpoint or async gate. Recalculates
 * automatically on window resize, when the nearest scrollable ancestor resizes,
 * and when the element's parent resizes — the parent is sized by its content,
 * so it changes whenever something above the element grows, shrinks or animates
 * open, which shifts the element's viewport position while the scroll ancestor's
 * own box stays the same.
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
    // Whole pixels: a fractional top would hand the consumer a fractional
    // height, and a table sized to 431.328px lays out no better than one at 431.
    setHeight(
      Math.max(300, Math.floor(window.innerHeight - top - bottomOffset)),
    )
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
      setHeight(
        Math.max(300, Math.floor(window.innerHeight - top - bottomOffset)),
      )

      // Observe what moves this element: the nearest scrollable ancestor's box,
      // and the element's own parent. A scroll container is often sized to the
      // viewport, so content growing above the element never changes its box;
      // the parent, sized by its content, changes on every frame of a section
      // animating open or closed above, and the last callback lands once the
      // layout has settled. Resizing the element itself in response changes the
      // parent again, but the recalculation then reads the same top and the
      // observer goes quiet.
      const scrollParent = findScrollParent(node)
      const parent = node.parentElement
      if (scrollParent || parent) {
        roRef.current = new ResizeObserver(calculate)
        if (scrollParent) roRef.current.observe(scrollParent)
        if (parent && parent !== scrollParent) roRef.current.observe(parent)
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
