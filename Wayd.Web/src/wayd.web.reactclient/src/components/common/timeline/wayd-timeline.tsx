'use client'

// timeline/wayd-timeline.tsx — public component. Variant-driven; wires the
// scale + layout strategy + render layer. Built in parallel to the existing
// WaydTimeline (no facade swap) so pages migrate one at a time.
//
// Layout: an antd Splitter divides a left pane (group labels) from a right pane
// (axis + chart). Users drag the splitter to resize the label column — no
// separate "group width" control needed. Each pane is a vertical stack
// [header | scroll body]; the two bodies' vertical scroll is kept in sync.

import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Spin, Splitter } from 'antd'
import { captureTimeline } from './render/capture-timeline'
import { createTimeScale } from './core/scale'
import {
  clampZoom,
  maxZoom,
  anchoredScrollLeft,
  initialScrollLeft,
} from './core/zoom'
import { getLayoutStrategy } from './layout'
import { ChartCanvas, type ChartCanvasProps } from './render/chart-canvas'
import { GroupColumn } from './render/group-column'
import { Axis } from './render/axis'
import TimelineToolbar from './render/timeline-toolbar'
import DrillControl from './render/drill-control'
import { resolveLevel } from './core/depth'
import { growRowsForLabels, type GeometryConfig } from './core/geometry'
import { truncateOneDayLabel } from './core/labels'
import { getVisibleRange } from './core/virtualization'
import type { TimelineGroup, TimelineItem } from './core/types'
import type { WaydTimelineProps } from './types'
import styles from './render/timeline.module.css'

const AXIS_HEIGHT = 48
const DEFAULT_HEIGHT = 600
const DEFAULT_LANE_HEIGHT = 28
const COMPACT_LANE_HEIGHT = 20
const DEFAULT_GROUP_COLUMN_WIDTH = 220
const MIN_GROUP_COLUMN_WIDTH = 120
const LANE_PADDING = 3
const ROW_PADDING = 6
const MIN_PX_PER_DAY = 3
const DAY_MS = 86_400_000
const ONE_DAY_LABEL_GAP = 4
const ONE_DAY_LABEL_EXTRA_PX = 8
// Minimum visible time span when fully zoomed in (1 day), matching the legacy
// timeline's `zoomMin`.
const ZOOM_MIN_MS = DAY_MS
// Multiplier applied per zoom-in/out step (buttons) and per wheel notch.
const ZOOM_STEP = 1.2
const ZOOM_WHEEL_STEP = 1.0015

/** Read a persisted group-column width, or fall back to the default. Returns the
 *  default (no read) when there's no key or storage isn't available. */
function readPersistedWidth(key: string | null, fallback: number): number {
  if (!key || typeof window === 'undefined') return fallback
  try {
    const raw = window.localStorage.getItem(key)
    if (raw == null) return fallback
    const parsed = Number(JSON.parse(raw))
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
  } catch {
    return fallback
  }
}

export function WaydTimeline<TItem = unknown, TGroup = unknown>(
  props: WaydTimelineProps<TItem, TGroup>,
) {
  const {
    variant = 'timeline',
    items,
    groups,
    windowStart,
    windowEnd,
    minDate,
    maxDate,
    height = DEFAULT_HEIGHT,
    laneHeight = DEFAULT_LANE_HEIGHT,
    groupColumnWidth = DEFAULT_GROUP_COLUMN_WIDTH,
    storageKey,
    defaultDrillLevel = 2,
    showCurrentTime: showCurrentTimeDefault = true,
    allowToggleCurrentTime = true,
    allowFullScreen = false,
    allowSaveAsImage = false,
    allowZoom = true,
    saveImageFileName = 'timeline',
    toolbarLeftSlot,
    toolbarRightSlot,
    onRefresh,
    itemRenderer,
    groupRenderer,
    onItemClick,
    editable = false,
    onItemDateChange,
    onItemProgressChange,
    isLoading,
    emptyMessage = 'No items to display',
  } = props

  const hasGroups = !!groups && groups.length > 0

  // The drill-level model is opt-in: an adapter signals it by setting explicit
  // `treeLevel` values on items or groups. This is distinct from structural
  // hierarchy (parentId on groups, which Gantt will also use) — a Gantt tree is
  // hierarchical but every item still gets its own row with no level filtering.
  // Flat peer groups (PI objectives) and future Gantt consumers never set
  // treeLevel, so they bypass resolveLevel and the drill controls entirely.
  const usesDrillLevel =
    hasGroups &&
    (groups!.some((g) => g.treeLevel != null) ||
      items.some((i) => i.treeLevel != null))

  // Drill-through level (1-based). Only meaningful when usesDrillLevel is true.
  // maxLevel = deepest treeLevel present across groups and items.
  const maxLevel = usesDrillLevel
    ? Math.max(
        1,
        ...groups!.map((g) => g.treeLevel ?? 0),
        ...items.map((i) => i.treeLevel ?? 0),
      )
    : 1
  const [userLevel, setUserLevel] = useState<number | undefined>(undefined)
  const level = Math.min(userLevel ?? defaultDrillLevel, maxLevel)

  const hasDrill = usesDrillLevel && maxLevel > 1
  const hasToolbar =
    allowFullScreen ||
    allowSaveAsImage ||
    allowZoom ||
    hasDrill ||
    !!onRefresh ||
    !!toolbarLeftSlot ||
    !!toolbarRightSlot

  // wrapperRef = toolbar + chart (fullscreen target). chartRootRef = just the
  // bordered chart container (save-as-image target, so the toolbar is excluded).
  const wrapperRef = useRef<HTMLDivElement>(null)
  const chartRootRef = useRef<HTMLDivElement>(null)
  const [isFullScreen, setIsFullScreen] = useState(false)

  // User-togglable current-time line (settings menu); seeded from the prop.
  const [showCurrentTime, setShowCurrentTime] = useState(showCurrentTimeDefault)
  // User-togglable vertical gridlines (settings menu); default on.
  const [showVerticalGridlines, setShowVerticalGridlines] = useState(true)
  // User-togglable weekend shading (settings menu); default off.
  const [showWeekends, setShowWeekends] = useState(false)
  // User-togglable compact mode (settings menu); default off.
  const [isCompact, setIsCompact] = useState(false)
  const effectiveLaneHeight = isCompact ? COMPACT_LANE_HEIGHT : laneHeight

  // Group-column width. Self-persisted per instance when `storageKey` is set, so
  // a user's splitter resize survives reloads with no consumer wiring; otherwise
  // it's plain ephemeral state seeded from the prop default. When there's no
  // storageKey we never touch localStorage at all (no shared/`__none__` key).
  const widthStorageKey = storageKey
    ? `wayd-timeline:groupWidth:${storageKey}`
    : null
  const [columnWidth, setColumnWidthState] = useState(() =>
    readPersistedWidth(widthStorageKey, groupColumnWidth),
  )
  // Re-read when the instance key changes without a remount (state-during-render
  // pattern), so switching roadmaps applies the new instance's saved width.
  const [prevWidthKey, setPrevWidthKey] = useState(widthStorageKey)
  if (prevWidthKey !== widthStorageKey) {
    setPrevWidthKey(widthStorageKey)
    setColumnWidthState(readPersistedWidth(widthStorageKey, groupColumnWidth))
  }
  const setColumnWidth = (width: number) => {
    setColumnWidthState(width)
    if (widthStorageKey) {
      try {
        window.localStorage.setItem(widthStorageKey, JSON.stringify(width))
      } catch {
        // Ignore quota/availability errors — width just won't persist.
      }
    }
  }

  // Measured group-label heights (by groupId) → rows grow to fit wrapped labels.
  const [labelHeights, setLabelHeights] = useState<Map<string, number>>(
    () => new Map(),
  )
  const onMeasureLabels = (heights: Map<string, number>) => {
    setLabelHeights((prev) => {
      // Merge (don't replace): keep previously-measured heights and overlay the
      // new readings so a partial measurement never drops a row's grown height.
      let changed = false
      for (const [k, v] of heights) {
        if (prev.get(k) !== v) {
          changed = true
          break
        }
      }
      if (!changed) return prev
      const next = new Map(prev)
      for (const [k, v] of heights) next.set(k, v)
      return next
    })
  }

  // "Fullscreen" here means fill the browser's current viewport (not the OS
  // monitor) — a CSS fixed-position overlay, matching the legacy timeline.
  // Native requestFullscreen would take over the whole monitor, which isn't
  // what we want. Esc exits.
  const toggleFullScreen = () => setIsFullScreen((v) => !v)

  useEffect(() => {
    if (!isFullScreen) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsFullScreen(false)
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [isFullScreen])

  const saveImage = () => {
    // Force every row into the DOM first (virtualization is normally on), then
    // capture once the full-height layout has painted, and turn windowing back on.
    setIsCapturing(true)
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        const el = chartRootRef.current
        if (!el) {
          setIsCapturing(false)
          return
        }
        const cs = getComputedStyle(el)
        const containerBg =
          cs.getPropertyValue('--ant-color-bg-container').trim() || undefined

        // We capture the CURRENT horizontal viewport (no time scrolled off-screen)
        // but the FULL vertical extent (all rows). Since we don't expand
        // horizontally, the Splitter layout stays intact — so we capture `.root`
        // as a single element (no split/stitch) and only un-clip the vertical
        // scroll in html2canvas's cloned document. The header underline etc.
        // render naturally because it's the real layout, just taller.
        const fullHeight = AXIS_HEIGHT + totalHeight + 2 // +2 for top/bottom border

        // The Splitter sizes its panels via JS; those widths don't survive the
        // html2canvas clone, so capture the live group-column width and re-pin it.
        const groupPane = el.querySelector<HTMLElement>(
          '[data-timeline-group-pane]',
        )
        const groupPaneWidth = groupPane
          ? Math.round(groupPane.getBoundingClientRect().width)
          : undefined

        captureTimeline(el, {
          fileName: `${saveImageFileName}.png`,
          backgroundColor: containerBg,
          captureHeight: fullHeight,
          groupPaneWidth,
        }).finally(() => setIsCapturing(false))
      })
    })
  }

  // Measure the chart viewport width so the time scale maps onto real pixels.
  // Use a CALLBACK ref: it fires exactly when the chart node attaches, which
  // removes all timing ambiguity around when the (Splitter-nested) panel mounts.
  const chartRef = useRef<HTMLDivElement | null>(null)
  const observerRef = useRef<ResizeObserver | null>(null)
  const scrollRafRef = useRef<number | null>(null)
  const axisViewportRef = useRef<HTMLDivElement>(null)
  const groupBodyRef = useRef<HTMLDivElement>(null)
  const [viewportWidth, setViewportWidth] = useState(0)
  const [viewportHeight, setViewportHeight] = useState(0)
  const [scrollLeft, setScrollLeft] = useState(0)
  const [scrollTop, setScrollTop] = useState(0)
  // While true, virtualization is bypassed so all rows render for image capture.
  const [isCapturing, setIsCapturing] = useState(false)
  // Horizontal time zoom: factor over the base (fit-to-window) width. 1 = reset.
  const [zoom, setZoom] = useState(1)
  // scrollLeft to apply on the NEXT paint after a zoom changes chartWidth, so the
  // anchor (cursor/centre) stays fixed. Consumed in a layout effect below.
  const pendingScrollLeftRef = useRef<number | null>(null)
  // Latest zoom geometry, refreshed every render so the (stable) wheel listener
  // attached in setChartRef reads current values without a stale closure.
  const zoomGeomRef = useRef({
    zoom: 1,
    baseWidth: 0,
    chartWidth: 0,
    bounds: { min: 1, max: 1 },
    allowZoom: true,
  })

  // While true, every resize re-locks the scroll to windowStart (the "home" view).
  // Set to false the first time the user pans or zooms.
  // Ref for synchronous reads in hot-path handlers (scroll, pan, zoom).
  // State mirrors it for the render (canReset prop).
  const isInitialViewRef = useRef(true)
  const [isInitialView, setIsInitialView] = useState(true)
  // Reset to the home view whenever the storageKey changes (different timeline
  // instance). Uses the state-during-render pattern (same as prevWidthKey above)
  // so React can bail out without a double-render from an effect.
  const [prevStorageKey, setPrevStorageKey] = useState(storageKey)
  if (prevStorageKey !== storageKey) {
    setPrevStorageKey(storageKey)
    setIsInitialView(true)
  }
  // The isInitialViewRef mutation must stay in an effect — refs can't be written
  // during render. Keep it in sync with the state transition above.
  const prevStorageKeyRef = useRef(storageKey)
  useLayoutEffect(() => {
    if (prevStorageKeyRef.current !== storageKey) {
      prevStorageKeyRef.current = storageKey
      isInitialViewRef.current = true
    }
  }, [storageKey])

  // Latest domain/window geometry — updated in a layout effect each render so
  // the (stable) scroll/reset callbacks always read current props without a
  // stale closure.
  const windowGeomRef = useRef({
    domainStart: 0,
    domainMs: 1,
    windowStart: 0,
    windowMs: 1,
  })

  // Resolve the drill level: which activities stay groups vs. demote to bars,
  // remapping every item to its nearest surviving group. (timeline variant only;
  // gantt keeps one row per record.) Backgrounds are split AFTER remapping so a
  // row-scoped timebox follows its reassigned group.
  const resolved =
    usesDrillLevel && variant === 'timeline'
      ? resolveLevel(items, groups!, level)
      : { items, groups: groups ?? [] }

  // ── Horizontal time geometry (domain + zoom) ──────────────────────────────
  // The render domain is the HARD BOUNDS [minDate, maxDate], defaulting to the
  // view window. This is also the pan/zoom limit, so
  // the axis matches the consumer's bounds exactly (e.g. the roadmap dates).
  // Items extending past the bounds are clipped at the canvas edge; we do NOT
  // widen the domain to fit them.
  // Domain must fully contain the window so the canvas is always wide enough to
  // scroll windowStart to the left edge with windowEnd visible at the right.
  const domainStart = Math.min(minDate ?? windowStart, windowStart)
  const domainEnd = Math.max(domainStart + 1, maxDate ?? windowEnd, windowEnd)
  const domainMs = domainEnd - domainStart
  const windowMs = Math.max(1, windowEnd - windowStart)
  // Base width is defined so that zoom=1 shows exactly the window
  // (windowStart→windowEnd) in the viewport. The full canvas is wider by the
  // domain/window ratio so the user can pan into minDate/maxDate on either side.
  // We do NOT apply a per-day minimum here — that would make multi-year windows
  // wider than the viewport at zoom=1. Users zoom in if they need finer detail.
  const baseWidth = Math.max(viewportWidth, 1) * (domainMs / windowMs)
  // zoomMin: the factor that fits the entire domain into the viewport (zoom out
  // floor). At this zoom, chartWidth === viewportWidth and the user can see
  // minDate→maxDate without scrolling. Always <= 1 since baseWidth >= viewportWidth.
  const zoomMin = domainMs > 0 ? windowMs / domainMs : 1
  // zoomMax: cap zoom-in so the viewport never spans less than ZOOM_MIN_MS (1 day).
  const zoomMax = maxZoom(baseWidth, viewportWidth, domainMs, ZOOM_MIN_MS)
  const effectiveZoom = clampZoom(zoom, { min: zoomMin, max: zoomMax })
  const chartWidth = baseWidth * effectiveZoom

  const wheelCleanupRef = useRef<(() => void) | null>(null)
  const setChartRef = (el: HTMLDivElement | null) => {
    chartRef.current = el
    observerRef.current?.disconnect()
    wheelCleanupRef.current?.()
    wheelCleanupRef.current = null
    if (!el) return
    const measure = () => {
      setViewportWidth(el.clientWidth)
      setViewportHeight(el.clientHeight)
      // Initial scroll is handled by the useLayoutEffect that watches viewportWidth.
    }
    measure()
    requestAnimationFrame(measure)
    const observer = new ResizeObserver(measure)
    observer.observe(el)
    observerRef.current = observer

    // Ctrl/Cmd + wheel = zoom, anchored on the cursor. Must be a NON-passive
    // listener so we can preventDefault the browser's pinch/scroll-zoom. Plain
    // wheel (no modifier) falls through to native horizontal/vertical scroll.
    const onWheel = (e: WheelEvent) => {
      const geom = zoomGeomRef.current
      if (!geom.allowZoom) return
      if (!e.ctrlKey && !e.metaKey) return
      e.preventDefault()
      const anchorX = e.clientX - el.getBoundingClientRect().left
      // Wheel up (deltaY < 0) zooms in; scale exponentially by the scroll amount.
      const factor = Math.pow(ZOOM_WHEEL_STEP, -e.deltaY)
      requestZoom(geom.zoom * factor, anchorX)
    }
    el.addEventListener('wheel', onWheel, { passive: false })
    wheelCleanupRef.current = () => el.removeEventListener('wheel', onWheel)
  }

  useEffect(
    () => () => {
      observerRef.current?.disconnect()
      wheelCleanupRef.current?.()
      if (scrollRafRef.current != null)
        cancelAnimationFrame(scrollRafRef.current)
    },
    [],
  )

  // After every render, publish current zoom geometry for the (stable) wheel
  // listener and toolbar handlers to read — and, when a zoom changed chartWidth,
  // set scrollLeft so the anchor stays fixed. useLayoutEffect runs before paint,
  // so the user never sees an intermediate (mis-scrolled) frame. Also syncs the
  // axis viewport (it mirrors chart scroll).
  useLayoutEffect(() => {
    windowGeomRef.current = { domainStart, domainMs, windowStart, windowMs }
    zoomGeomRef.current = {
      zoom: effectiveZoom,
      baseWidth,
      chartWidth,
      bounds: { min: zoomMin, max: zoomMax },
      allowZoom,
    }
    const pending = pendingScrollLeftRef.current
    if (pending == null) return
    pendingScrollLeftRef.current = null
    const chart = chartRef.current
    if (chart) {
      chart.scrollLeft = pending
      if (axisViewportRef.current) axisViewportRef.current.scrollLeft = pending
      setScrollLeft(pending)
    }
  }, [
    effectiveZoom,
    baseWidth,
    chartWidth,
    zoomMin,
    zoomMax,
    allowZoom,
    domainStart,
    domainMs,
    windowStart,
    windowMs,
  ])

  // While in the initial view (no user pan/zoom), lock scroll to windowStart
  // on every render where the viewport is measured. Runs in useLayoutEffect so
  // it applies before paint — no flash of wrong position.
  useLayoutEffect(() => {
    if (!isInitialViewRef.current) return
    const chart = chartRef.current
    if (!chart) return
    if (chart.clientWidth <= 0) return
    const offset = initialScrollLeft(
      domainStart,
      domainMs,
      windowStart,
      baseWidth,
    )
    // Only apply if the canvas is wide enough to scroll to this offset.
    if (chart.scrollWidth <= offset) return
    chart.scrollLeft = offset
    if (axisViewportRef.current) axisViewportRef.current.scrollLeft = offset
    // Defer state update to avoid setState-in-effect lint error; bar labels
    // tolerate a one-frame lag after a programmatic scroll to windowStart.
    requestAnimationFrame(() => setScrollLeft(offset))
  }, [viewportWidth, domainStart, domainMs, windowStart, baseWidth, chartWidth])

  // Chart scroll drives: axis horizontal sync + group-column vertical sync, and
  // a debounced scrollLeft state so bar labels can stick to the visible left
  // edge (rAF-throttled to avoid re-rendering bars on every scroll event).
  const onChartScroll = () => {
    const chart = chartRef.current
    if (!chart) return
    // Any scroll (including Shift+wheel / trackpad horizontal pan) means the user
    // has navigated away from the default window — clear the initial-view lock so
    // a later viewport resize doesn't snap back to windowStart.
    if (isInitialViewRef.current) {
      isInitialViewRef.current = false
      setIsInitialView(false)
    }
    if (axisViewportRef.current) {
      axisViewportRef.current.scrollLeft = chart.scrollLeft
    }
    if (groupBodyRef.current) {
      groupBodyRef.current.scrollTop = chart.scrollTop
    }
    if (scrollRafRef.current == null) {
      scrollRafRef.current = requestAnimationFrame(() => {
        scrollRafRef.current = null
        const c = chartRef.current
        setScrollLeft(c?.scrollLeft ?? 0)
        setScrollTop(c?.scrollTop ?? 0)
      })
    }
  }

  // Group-column scroll (wheel over labels) drives the chart vertically.
  const onGroupScroll = () => {
    const group = groupBodyRef.current
    if (group && chartRef.current) {
      chartRef.current.scrollTop = group.scrollTop
    }
  }

  // Drag-to-pan: grab empty chart space and drag to scroll horizontally (and
  // vertically). Bars/handles/milestones stop pointer propagation (or are marked
  // with data-timeline-item), so this only engages on whitespace — the native
  // scrollbar still works alongside it.
  const panSessionRef = useRef<{
    pointerId: number
    startX: number
    startY: number
    startScrollLeft: number
    startScrollTop: number
    moved: boolean
  } | null>(null)

  const onPanPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    // Primary button only; ignore clicks that land on an interactive item.
    if (e.button !== 0) return
    if ((e.target as HTMLElement).closest('[data-timeline-item]')) return
    isInitialViewRef.current = false
    setIsInitialView(false)
    const chart = chartRef.current
    if (!chart) return
    // Nothing to pan if content fits the viewport in both axes.
    const canScroll =
      chart.scrollWidth > chart.clientWidth ||
      chart.scrollHeight > chart.clientHeight
    if (!canScroll) return

    // Avoid starting a native text/element selection while dragging to pan.
    e.preventDefault()

    panSessionRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      startScrollLeft: chart.scrollLeft,
      startScrollTop: chart.scrollTop,
      moved: false,
    }
    chart.style.cursor = 'grabbing'

    const move = (ev: PointerEvent) => {
      const s = panSessionRef.current
      const c = chartRef.current
      if (!s || !c) return
      const dx = ev.clientX - s.startX
      const dy = ev.clientY - s.startY
      if (!s.moved && Math.abs(dx) + Math.abs(dy) >= 3) s.moved = true
      // Dragging right reveals content to the left → scrollLeft decreases.
      c.scrollLeft = s.startScrollLeft - dx
      c.scrollTop = s.startScrollTop - dy
    }
    const up = () => {
      const c = chartRef.current
      if (c) c.style.cursor = ''
      panSessionRef.current = null
      window.removeEventListener('pointermove', move)
      window.removeEventListener('pointerup', up)
    }
    window.addEventListener('pointermove', move)
    window.addEventListener('pointerup', up)
  }

  // Apply a new zoom factor, keeping the time under `anchorX` (viewport-local px)
  // fixed. Reads current geometry from the ref so it's safe to call from both the
  // wheel listener and the toolbar buttons. Stages the post-paint scrollLeft.
  const requestZoom = (nextZoomRaw: number, anchorX: number) => {
    isInitialViewRef.current = false
    setIsInitialView(false)
    const geom = zoomGeomRef.current
    const next = clampZoom(nextZoomRaw, geom.bounds)
    if (next === geom.zoom) return
    const newWidth = geom.baseWidth * next
    pendingScrollLeftRef.current = anchoredScrollLeft({
      oldWidth: geom.chartWidth,
      newWidth,
      oldScrollLeft: chartRef.current?.scrollLeft ?? 0,
      anchorX,
      viewportWidth: chartRef.current?.clientWidth ?? 0,
    })
    setZoom(next)
  }

  // Zoom in/out by a fixed step, anchored on the viewport centre (toolbar buttons).
  const zoomBy = (factor: number) => {
    const geom = zoomGeomRef.current
    const centre = (chartRef.current?.clientWidth ?? 0) / 2
    requestZoom(geom.zoom * factor, centre)
  }

  // Reset View: zoom=1 (window fills viewport) and scroll to windowStart.
  const resetView = () => {
    isInitialViewRef.current = true
    setIsInitialView(true)
    const g = windowGeomRef.current
    const chart = chartRef.current
    const w = chart?.clientWidth ?? viewportWidth
    const offset =
      g.windowMs > 0 ? ((g.windowStart - g.domainStart) / g.windowMs) * w : 0
    // Always apply the scroll immediately via DOM refs so panning-only resets
    // work even when zoom is already 1 (setZoom(1) would be a no-op → no
    // layout effect → pendingScrollLeftRef never consumed).
    if (chart) {
      chart.scrollLeft = offset
      if (axisViewportRef.current) axisViewportRef.current.scrollLeft = offset
      setScrollLeft(offset)
    }
    // Also stage via pendingScrollLeftRef as a fallback for when the zoom
    // changes (layout effect fires on the next paint and re-applies it).
    pendingScrollLeftRef.current = offset
    setZoom(1)
  }

  // Loading: show a spinner until data arrives (covers the isLoading + no-data
  // window, which would otherwise render a blank zero-height chart).
  if (isLoading && items.length === 0) {
    return (
      <div className={styles.root} style={{ height }}>
        <div className={styles.empty}>
          <Spin />
        </div>
      </div>
    )
  }

  if (!isLoading && items.length === 0) {
    return (
      <div className={styles.root} style={{ height }}>
        <div className={styles.empty}>{emptyMessage}</div>
      </div>
    )
  }

  const chartBackgrounds = resolved.items.filter((i) => i.kind === 'background')
  const layoutItems = resolved.items.filter((i) => i.kind !== 'background')

  // Group ids that have a row-scoped background → reserve label headroom in those
  // rows so the timebox name sits above the bars. Chart-wide (ungrouped)
  // backgrounds drive headroom on the flat row instead.
  const backgroundGroupIds = new Set<string>()
  let hasChartBackground = false
  for (const bg of chartBackgrounds) {
    if (bg.groupId) backgroundGroupIds.add(bg.groupId)
    else hasChartBackground = true
  }

  const scale = createTimeScale(domainStart, domainEnd, chartWidth)
  const oneDayMarkerSize = effectiveLaneHeight - LANE_PADDING * 2
  const oneDayLabelFontSize = Math.max(
    9,
    Math.min(Math.floor(oneDayMarkerSize / 1.2), 13),
  )
  const estimateOneDayLabelWidth = (label: string) =>
    label.length * oneDayLabelFontSize * 0.58 + ONE_DAY_LABEL_EXTRA_PX
  const getCollisionEnd = (item: TimelineItem) => {
    if (item.kind !== 'range' || item.end - item.start > DAY_MS) {
      return item.kind === 'milestone' ? item.start : item.end
    }

    const rawX1 = scale.toX(item.start)
    const rawX2 = scale.toX(item.end)
    const spanWidth = Math.max(oneDayMarkerSize, rawX2 - rawX1)
    const markerLeft = rawX1 + Math.max(0, (spanWidth - oneDayMarkerSize) / 2)
    const label = truncateOneDayLabel(item.label ?? item.id)
    const labelRight =
      markerLeft +
      oneDayMarkerSize +
      ONE_DAY_LABEL_GAP +
      estimateOneDayLabelWidth(label)

    return Math.max(item.end, scale.toMs(labelRight))
  }

  const strategy = getLayoutStrategy(variant)
  const base = strategy({
    items: layoutItems,
    groups: resolved.groups,
    backgroundGroupIds,
    hasChartBackground,
    getCollisionEnd,
    config: { laneHeight: effectiveLaneHeight, rowPadding: ROW_PADDING },
  })

  // Grow rows whose wrapped group label is taller than the lane-based height,
  // using heights measured in the rendered column (see GroupColumn onMeasure).
  const { rows, totalHeight } = growRowsForLabels(base.rows, labelHeights)

  // Virtualize: only rows intersecting the viewport (plus overscan) render.
  // The full totalHeight is kept as a spacer so the scrollbar reflects all rows.
  // During an image capture we bypass windowing so EVERY row is in the DOM (the
  // capture renders the full vertical extent, including rows scrolled off-view).
  const visibleRange = isCapturing
    ? { startIndex: 0, endIndex: rows.length - 1 }
    : getVisibleRange(rows, scrollTop, viewportHeight)
  const visibleRows =
    visibleRange.endIndex < 0
      ? []
      : rows.slice(visibleRange.startIndex, visibleRange.endIndex + 1)

  const geometry: GeometryConfig = {
    laneHeight: effectiveLaneHeight,
    lanePadding: LANE_PADDING,
    rowPadding: ROW_PADDING,
  }
  const groupsById = new Map<string, TimelineGroup>(
    resolved.groups.map((g) => [g.id, g]),
  )

  // Show the group column + splitter only when the resolved level actually has
  // groups. At drill level 1 there are none → render a flat, full-width chart.
  const showGroupColumn = resolved.groups.length > 0

  // The right pane (axis + chart) is shared by both grouped/ungrouped layouts.
  const rightPane = (
    <div className={styles.pane}>
      <div className={styles.axisViewport} ref={axisViewportRef}>
        {viewportWidth > 0 && <Axis scale={scale} height={AXIS_HEIGHT} />}
      </div>
      <div
        className={styles.chartScroll}
        ref={setChartRef}
        onScroll={onChartScroll}
        onPointerDown={onPanPointerDown}
      >
        {viewportWidth > 0 && (
          <ChartCanvas
            rows={visibleRows}
            allRows={rows}
            rowIndexOffset={visibleRange.startIndex}
            totalHeight={totalHeight}
            scale={scale}
            geometry={geometry}
            chartBackgrounds={chartBackgrounds}
            editable={editable}
            viewportLeft={scrollLeft}
            itemRenderer={itemRenderer as ChartCanvasProps['itemRenderer']}
            onItemClick={onItemClick as ChartCanvasProps['onItemClick']}
            onItemDateChange={onItemDateChange}
            onItemProgressChange={onItemProgressChange}
            showCurrentTime={showCurrentTime}
            showGridlines={showVerticalGridlines}
            showWeekends={showWeekends}
            canPan={
              chartWidth > viewportWidth + 1 || totalHeight > viewportHeight + 1
            }
            dragMin={domainStart}
            dragMax={domainEnd}
          />
        )}
      </div>
    </div>
  )

  return (
    <div
      ref={wrapperRef}
      className={`${styles.wrapper} ${isFullScreen ? styles.fullscreen : ''}`}
    >
      {hasToolbar && (
        <TimelineToolbar
          leftSlot={
            <>
              {hasDrill && (
                <DrillControl
                  level={level}
                  maxLevel={maxLevel}
                  onChange={setUserLevel}
                />
              )}
              {toolbarLeftSlot}
            </>
          }
          rightSlot={toolbarRightSlot}
          allowZoom={allowZoom}
          onZoomIn={() => zoomBy(ZOOM_STEP)}
          onZoomOut={() => zoomBy(1 / ZOOM_STEP)}
          onResetView={resetView}
          canZoomIn={effectiveZoom < zoomMax - 1e-6}
          canZoomOut={effectiveZoom > zoomMin + 1e-6}
          canReset={Math.abs(effectiveZoom - 1) > 1e-6 || !isInitialView}
          allowSaveAsImage={allowSaveAsImage}
          onSaveAsImage={saveImage}
          allowFullScreen={allowFullScreen}
          isFullScreen={isFullScreen}
          onToggleFullScreen={toggleFullScreen}
          allowSettings={allowToggleCurrentTime}
          showCurrentTime={showCurrentTime}
          onToggleCurrentTime={setShowCurrentTime}
          showVerticalGridlines={showVerticalGridlines}
          onToggleVerticalGridlines={setShowVerticalGridlines}
          showWeekends={showWeekends}
          onToggleWeekends={setShowWeekends}
          isCompact={isCompact}
          onToggleCompact={setIsCompact}
          onRefresh={onRefresh}
          editable={editable}
        />
      )}
      <div
        ref={chartRootRef}
        className={styles.root}
        style={isFullScreen ? undefined : { height }}
      >
        {showGroupColumn ? (
          <Splitter
            // antd Splitter reads `defaultSize` only on mount; remount when the
            // instance (storageKey) changes so a different roadmap's persisted
            // width is applied.
            key={storageKey ?? 'default'}
            // Persist the resized group-column width (first panel) on drop. Using
            // onResizeEnd (not onResize) avoids writing to storage on every frame.
            onResizeEnd={(sizes) => {
              const next = sizes[0]
              if (typeof next === 'number' && next > 0) setColumnWidth(next)
            }}
          >
            <Splitter.Panel
              defaultSize={columnWidth}
              min={MIN_GROUP_COLUMN_WIDTH}
              max="60%"
            >
              <div className={styles.pane} data-timeline-group-pane>
                {/* Corner spacer aligns the column under the axis header. */}
                <div
                  className={styles.headerCorner}
                  style={{ height: AXIS_HEIGHT }}
                />
                <div
                  className={styles.groupBody}
                  ref={groupBodyRef}
                  onScroll={onGroupScroll}
                >
                  <GroupColumn
                    rows={rows}
                    totalHeight={totalHeight}
                    width="100%"
                    groupsById={groupsById}
                    groupRenderer={
                      groupRenderer as React.ComponentProps<
                        typeof GroupColumn
                      >['groupRenderer']
                    }
                    onMeasure={onMeasureLabels}
                    laneHeight={effectiveLaneHeight}
                  />
                </div>
              </div>
            </Splitter.Panel>
            <Splitter.Panel>{rightPane}</Splitter.Panel>
          </Splitter>
        ) : (
          rightPane
        )}
      </div>
    </div>
  )
}

export default WaydTimeline
