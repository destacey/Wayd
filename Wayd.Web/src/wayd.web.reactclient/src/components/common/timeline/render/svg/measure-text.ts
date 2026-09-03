// timeline/render/svg/measure-text.ts
// Real text measurement for the SVG export.
//
// SVG has no `text-overflow: ellipsis`, so the renderer must decide itself
// where a label stops fitting. A fixed characters-x-ratio estimate cannot do
// that job: measured against the app's own font, per-string ratios run from
// 0.24 ("iiii") to 0.93 ("WWWW"), so any single constant is badly wrong for
// most real labels — it truncates names that would have fit, and overflows the
// ones built from wide glyphs.
//
// Canvas `measureText` matches real DOM layout to within 0.01px (verified in a
// browser against laid-out spans), so it is used here as the source of truth.

/** Measures label widths for one font, caching results per (size, text). */
export interface TextMeasurer {
  width: (text: string, fontSize: number) => number
}

/** Ratio fallback used only when no canvas is available (SSR, jsdom). */
const FALLBACK_RATIO = 0.55

/**
 * A measurer backed by a 2D canvas using `fontFamily`.
 *
 * Falls back to a ratio estimate when a canvas context cannot be created —
 * the export still produces a file, just with the old approximate truncation.
 */
export function createTextMeasurer(fontFamily: string): TextMeasurer {
  let ctx: CanvasRenderingContext2D | null = null
  try {
    ctx = document.createElement('canvas').getContext('2d')
  } catch {
    ctx = null
  }

  if (!ctx) {
    return {
      width: (text, fontSize) => text.length * fontSize * FALLBACK_RATIO,
    }
  }

  const measuringCtx = ctx
  const cache = new Map<string, number>()
  return {
    width: (text, fontSize) => {
      const key = `${fontSize}:${text}`
      const hit = cache.get(key)
      if (hit !== undefined) return hit
      measuringCtx.font = `${fontSize}px ${fontFamily}`
      const w = measuringCtx.measureText(text).width
      cache.set(key, w)
      return w
    },
  }
}

/** A measurer that always uses the ratio estimate — for tests and SSR. */
export function createApproximateMeasurer(
  ratio = FALLBACK_RATIO,
): TextMeasurer {
  return { width: (text, fontSize) => text.length * fontSize * ratio }
}

/**
 * Truncate `text` with an ellipsis so it fits `maxWidth` px.
 * Binary search over the measurer, so cost is logarithmic in label length.
 */
export function truncateToWidth(
  text: string,
  maxWidth: number,
  fontSize: number,
  measurer: TextMeasurer,
): string {
  if (maxWidth <= 0) return ''
  if (measurer.width(text, fontSize) <= maxWidth) return text

  const ellipsis = '…'
  if (measurer.width(ellipsis, fontSize) > maxWidth) return ''

  let lo = 0
  let hi = text.length
  while (lo < hi) {
    const mid = Math.ceil((lo + hi) / 2)
    const candidate = text.slice(0, mid).trimEnd() + ellipsis
    if (measurer.width(candidate, fontSize) <= maxWidth) lo = mid
    else hi = mid - 1
  }
  return lo <= 0 ? ellipsis : text.slice(0, lo).trimEnd() + ellipsis
}

/**
 * Break `text` into at most `maxLines` lines that each fit `maxWidth` px,
 * ellipsising the last line if anything is left over. Words too long for a
 * line are broken mid-word, matching the live column's `overflow-wrap:
 * anywhere`.
 */
export function wrapToWidth(
  text: string,
  maxWidth: number,
  fontSize: number,
  maxLines: number,
  measurer: TextMeasurer,
): string[] {
  if (maxWidth <= 0 || maxLines <= 0) return []

  const fits = (s: string) => measurer.width(s, fontSize) <= maxWidth

  // Greedily lay the text out with no line budget, then apply the budget once.
  // Keeping the two steps separate is what makes the overflow case simple:
  // "did this need more lines than it had" is a single length comparison.
  const all: string[] = []
  let current = ''
  const words = text.trim().split(/\s+/).filter(Boolean)

  for (const rawWord of words) {
    let word = rawWord
    const candidate = current ? `${current} ${word}` : word
    if (fits(candidate)) {
      current = candidate
      continue
    }
    if (current) {
      all.push(current)
      current = ''
    }
    // A single word wider than the line is split mid-word, matching the live
    // column's `overflow-wrap: anywhere`.
    while (word && !fits(word)) {
      let cut = word.length
      while (cut > 1 && !fits(word.slice(0, cut))) cut -= 1
      all.push(word.slice(0, cut))
      word = word.slice(cut)
    }
    current = word
  }
  if (current) all.push(current)

  if (all.length === 0) return []
  if (all.length <= maxLines) return all

  // Over budget: keep the lines that fit and ellipsise the last kept one so the
  // reader can see the name was cut.
  const kept = all.slice(0, maxLines)
  const lastIndex = kept.length - 1
  kept[lastIndex] = truncateToWidth(
    `${kept[lastIndex]}…`,
    maxWidth,
    fontSize,
    measurer,
  )
  return kept
}
