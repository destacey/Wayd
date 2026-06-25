// timeline2/core/scale.ts
// Date<->pixel mapping + calendar-aware tick generation. Pure, no DOM/React.
//
// Originally planned on d3-scale (decision D-2), but d3's ESM-only packaging
// fights next/jest's transform pipeline (next/jest forces node_modules to be
// ignored unless transpilePackages is honored, which proved unreliable here).
// The mapping for a single contiguous time domain is just linear interpolation,
// and calendar-aware tick stepping is a few lines on dayjs (the repo's standard
// date lib, already a dependency). So we own this small, well-bounded math
// instead — same spirit as hand-rolling packing. See timeline-replacement spec.

import dayjs, { type Dayjs } from 'dayjs'

export interface TimeScale {
  /** epoch ms -> pixel x within [0, width]. */
  toX: (ms: number) => number
  /** pixel x -> epoch ms. */
  toMs: (x: number) => number
  /** Width of the pixel range. */
  width: number
  /** Pixels per millisecond (constant for a linear domain). */
  pxPerMs: number
  /** Visible domain as epoch ms [start, end]. */
  domain: [number, number]
  /** Generate ~`count` calendar-aligned ticks as epoch ms. */
  ticks: (count?: number) => number[]
  /** Two-tier header segments (upper = month/year, lower = week/day), span-aware. */
  tiers: () => { upper: ScaleSegment[]; lower: ScaleSegment[] }
}

/** A labeled header cell spanning [startMs, endMs). */
export interface ScaleSegment {
  startMs: number
  endMs: number
  label: string
}

type TickUnit = 'hour' | 'day' | 'week' | 'month' | 'year'

// Candidate step sizes, smallest -> largest, with approximate span in ms used
// only to pick a sensible granularity for the requested tick count.
const HOUR = 3_600_000
const DAY = 86_400_000
const TICK_STEPS: Array<{ unit: TickUnit; step: number; approxMs: number }> = [
  { unit: 'hour', step: 1, approxMs: HOUR },
  { unit: 'hour', step: 3, approxMs: 3 * HOUR },
  { unit: 'hour', step: 6, approxMs: 6 * HOUR },
  { unit: 'hour', step: 12, approxMs: 12 * HOUR },
  { unit: 'day', step: 1, approxMs: DAY },
  { unit: 'day', step: 2, approxMs: 2 * DAY },
  { unit: 'week', step: 1, approxMs: 7 * DAY },
  { unit: 'week', step: 2, approxMs: 14 * DAY },
  { unit: 'month', step: 1, approxMs: 30 * DAY },
  { unit: 'month', step: 3, approxMs: 91 * DAY },
  { unit: 'year', step: 1, approxMs: 365 * DAY },
]

/** Snap a dayjs value DOWN to the start of the given unit. */
function floorTo(d: Dayjs, unit: TickUnit): Dayjs {
  // dayjs treats 'week' via startOf('week'); others map directly.
  return d.startOf(unit === 'week' ? 'week' : unit)
}

/** Advance a dayjs value by step units. */
function addStep(d: Dayjs, unit: TickUnit, step: number): Dayjs {
  return d.add(step, unit)
}

/**
 * Build labeled segments for `unit` across [startMs, endMs], clamped to the
 * domain so the first/last cells don't overflow. `fmt` formats each cell.
 */
function segmentsFor(
  startMs: number,
  endMs: number,
  unit: TickUnit,
  fmt: (d: Dayjs) => string,
): ScaleSegment[] {
  const out: ScaleSegment[] = []
  let cursor = floorTo(dayjs(startMs), unit)
  let guard = 0
  while (cursor.valueOf() < endMs && guard < 2000) {
    const next = addStep(cursor, unit, 1)
    out.push({
      startMs: Math.max(cursor.valueOf(), startMs),
      endMs: Math.min(next.valueOf(), endMs),
      label: fmt(cursor),
    })
    cursor = next
    guard += 1
  }
  return out
}

export function createTimeScale(
  startMs: number,
  endMs: number,
  width: number,
): TimeScale {
  // Guard degenerate inputs so we never produce NaN/Infinity downstream.
  const safeWidth = Number.isFinite(width) && width > 0 ? width : 1
  const safeEnd = endMs > startMs ? endMs : startMs + 1
  const span = safeEnd - startMs
  const pxPerMs = safeWidth / span

  return {
    width: safeWidth,
    pxPerMs,
    domain: [startMs, safeEnd],
    toX: (ms) => (ms - startMs) * pxPerMs,
    toMs: (x) => startMs + x / pxPerMs,
    ticks: (count = 10) => {
      const target = Math.max(1, count)
      const desiredMs = span / target

      // Pick the smallest step whose span is >= the desired spacing.
      let chosen = TICK_STEPS[TICK_STEPS.length - 1]
      for (const candidate of TICK_STEPS) {
        if (candidate.approxMs >= desiredMs) {
          chosen = candidate
          break
        }
      }

      const out: number[] = []
      // Start at the first aligned boundary at or after the domain start.
      let cursor = floorTo(dayjs(startMs), chosen.unit)
      if (cursor.valueOf() < startMs) {
        cursor = addStep(cursor, chosen.unit, chosen.step)
      }
      // Guard against pathological loops.
      let guard = 0
      while (cursor.valueOf() <= safeEnd && guard < 1000) {
        out.push(cursor.valueOf())
        cursor = addStep(cursor, chosen.unit, chosen.step)
        guard += 1
      }
      return out
    },
    tiers: () => {
      // Pick tier granularities by PIXEL DENSITY (how wide one day renders), not
      // the total domain span — so granularity gets FINER as the user zooms in
      // (more px per day) and coarser as they zoom out, matching vis-timeline.
      //  wide day  (zoomed in):  month over day   (every day number)
      //  mid       :             month over week
      //  narrow    (zoomed out): year over month
      const pxPerDay = pxPerMs * DAY
      let upperUnit: TickUnit
      let lowerUnit: TickUnit
      let upperFmt: (d: Dayjs) => string
      let lowerFmt: (d: Dayjs) => string
      if (pxPerDay >= 25) {
        upperUnit = 'month'
        lowerUnit = 'day'
        upperFmt = (d) => d.format('MMMM YYYY')
        lowerFmt = (d) => d.format('D')
      } else if (pxPerDay >= 6) {
        upperUnit = 'month'
        lowerUnit = 'week'
        upperFmt = (d) => d.format('MMMM YYYY')
        lowerFmt = (d) => d.format('D')
      } else {
        upperUnit = 'year'
        lowerUnit = 'month'
        upperFmt = (d) => d.format('YYYY')
        lowerFmt = (d) => d.format('MMM')
      }
      return {
        upper: segmentsFor(startMs, safeEnd, upperUnit, upperFmt),
        lower: segmentsFor(startMs, safeEnd, lowerUnit, lowerFmt),
      }
    },
  }
}
