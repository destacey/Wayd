'use client'

// timeline2/wayd-timeline2.tsx — public component. Variant-driven; wires the
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
import { clampZoom, maxZoom, anchoredScrollLeft } from './core/zoom'
import { getLayoutStrategy } from './layout'
import { ChartCanvas, type ChartCanvasProps } from './render/chart-canvas'
import { GroupColumn } from './render/group-column'
import { Axis } from './render/axis'
import TimelineToolbar from './render/timeline-toolbar'
import DrillControl from './render/drill-control'
import { resolveLevel } from './core/depth'
import { growRowsForLabels, type GeometryConfig } from './core/geometry'
import { getVisibleRange } from './core/virtualization'
import type { TimelineGroup } from './core/types'
import type { WaydTimeline2Props } from './types'
import styles from './render/timeline.module.css'

const AXIS_HEIGHT = 48
const DEFAULT_HEIGHT = 600
const DEFAULT_LANE_HEIGHT = 28
const DEFAULT_GROUP_COLUMN_WIDTH = 220
const MIN_GROUP_COLUMN_WIDTH = 120
const LANE_PADDING = 3
const ROW_PADDING = 6
const MIN_PX_PER_DAY = 3
const DAY_MS = 86_400_000
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

export function WaydTimeline2<TItem = unknown, TGroup = unknown>(
  props: WaydTimeline2Props<TItem, TGroup>,
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

  // Group-column width. Self-persisted per instance when `storageKey` is set, so
  // a user's splitter resize survives reloads with no consumer wiring; otherwise
  // it's plain ephemeral state seeded from the prop default. When there's no
  // storageKey we never touch localStorage at all (no shared/`__none__` key).
  const widthStorageKey = storageKey
    ? `wayd-timeline2:groupWidth:${storageKey}`
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

  // Resolve the drill level: which activities stay groups vs. demote to bars,
  // remapping every item to its nearest surviving group. (timeline variant only;
  // gantt keeps one row per record.) Backgrounds are split AFTER remapping so a
  // row-scoped timebox follows its reassigned group.
  const resolved =
    usesDrillLevel && variant === 'timeline'
      ? resolveLevel(items, groups!, level)
      : { items, groups: groups ?? [] }

  // ── Horizontal time geometry (domain + zoom) ──────────────────────────────
  // The render domain is the HARD BOUNDS [minDate, maxDate] (vis-timeline's
  // min/max), defaulting to the view window. This is also the pan/zoom limit, so
  // the axis matches the consumer's bounds exactly (e.g. the roadmap dates).
  // Items extending past the bounds are clipped at the canvas edge; we do NOT
  // widen the domain to fit them.
  const domainStart = minDate ?? windowStart
  const domainEnd = Math.max(domainStart + 1, maxDate ?? windowEnd)
  const domainMs = domainEnd - domainStart
  const spanDays = Math.max(1, domainMs / DAY_MS)
  // Base width fits the whole domain into the viewport (or a per-day minimum).
  // Zoom scales it up; pan is native horizontal scroll over the widened content.
  const baseWidth = Math.max(viewportWidth, spanDays * MIN_PX_PER_DAY)
  // Cap zoom-in so the viewport never spans less than ZOOM_MIN_MS (1 day),
  // matching the legacy timeline's `zoomMin`.
  const zoomMin = 1
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
      if (scrollRafRef.current != null) cancelAnimationFrame(scrollRafRef.current)
    },
    [],
  )

  // After every render, publish current zoom geometry for the (stable) wheel
  // listener and toolbar handlers to read — and, when a zoom changed chartWidth,
  // set scrollLeft so the anchor stays fixed. useLayoutEffect runs before paint,
  // so the user never sees an intermediate (mis-scrolled) frame. Also syncs the
  // axis viewport (it mirrors chart scroll).
  useLayoutEffect(() => {
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
  }, [effectiveZoom, baseWidth, chartWidth, zoomMin, zoomMax, allowZoom])

  // Chart scroll drives: axis horizontal sync + group-column vertical sync, and
  // a debounced scrollLeft state so bar labels can stick to the visible left
  // edge (rAF-throttled to avoid re-rendering bars on every scroll event).
  const onChartScroll = () => {
    const chart = chartRef.current
    if (!chart) return
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

  // Reset View: back to fit-the-window (zoom 1) and scrolled to the start.
  const resetView = () => {
    pendingScrollLeftRef.current = 0
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

  const strategy = getLayoutStrategy(variant)
  const base = strategy({
    items: layoutItems,
    groups: resolved.groups,
    backgroundGroupIds,
    hasChartBackground,
    config: { laneHeight, rowPadding: ROW_PADDING },
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

  const scale = createTimeScale(domainStart, domainEnd, chartWidth)
  const geometry: GeometryConfig = { laneHeight, lanePadding: LANE_PADDING }
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
          canReset={effectiveZoom > 1 + 1e-6 || scrollLeft > 0.5}
          allowSaveAsImage={allowSaveAsImage}
          onSaveAsImage={saveImage}
          allowFullScreen={allowFullScreen}
          isFullScreen={isFullScreen}
          onToggleFullScreen={toggleFullScreen}
          allowSettings={allowToggleCurrentTime}
          showCurrentTime={showCurrentTime}
          onToggleCurrentTime={setShowCurrentTime}
          onRefresh={onRefresh}
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

export default WaydTimeline2
