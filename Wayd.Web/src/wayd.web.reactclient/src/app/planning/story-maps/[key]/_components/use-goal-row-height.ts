'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Measures the pinned goals row, so the steps row beneath it knows how far down to pin. A stale
 * offset either overlaps the two rows or leaves a strip of scrolled content between them.
 *
 * Attach the ref to the row's LABEL cell, not a goal cell: any one goal may be shorter than the
 * row, while `align-items: stretch` sizes the label to the tallest. Giving that cell its own height
 * or `align-self` would silently break this.
 */
export const useGoalRowHeight = (): [
  ref: (node: HTMLElement | null) => void,
  height: number,
] => {
  const [height, setHeight] = useState(0)
  const observerRef = useRef<ResizeObserver | null>(null)

  const measure = useCallback((node: HTMLElement) => {
    setHeight(node.getBoundingClientRect().height)
  }, [])

  const callbackRef = useCallback(
    (node: HTMLElement | null) => {
      observerRef.current?.disconnect()
      observerRef.current = null

      if (!node) return

      measure(node)

      // The row reflows whenever a name wraps differently — on resize, on rename, and when
      // collapsing a goal changes every column's width.
      observerRef.current = new ResizeObserver(() => measure(node))
      observerRef.current.observe(node)
    },
    [measure],
  )

  useEffect(() => () => observerRef.current?.disconnect(), [])

  return [callbackRef, height]
}
