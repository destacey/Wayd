'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Measures the pinned goals row, so the steps row beneath it knows how far down to pin.
 *
 * The offset cannot be a constant: the goals row is as tall as its longest wrapped goal name, which
 * changes with the name, the column width, and the viewport. Measuring keeps the two pinned rows
 * flush — a stale offset either overlaps them or leaves a strip of scrolled content between.
 *
 * Returns a callback ref for any cell in the goals row, plus its measured height.
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
