// timeline/render/svg/theme.ts
// Resolves the antd design tokens the timeline draws with into literal colors.
//
// The live chart styles itself entirely through CSS variables (per the repo's
// theming rule). An exported SVG is a standalone document with no antd
// stylesheet, so every var must be flattened to a concrete value at export
// time — read from the live element so the export matches the user's ACTIVE
// theme (light/dark) rather than a hardcoded palette.

/** The token set the SVG renderer needs, flattened to literal CSS colors. */
export interface SvgTheme {
  text: string
  textSecondary: string
  border: string
  split: string
  bgContainer: string
  bgElevated: string
  fillQuaternary: string
  fillSecondary: string
  error: string
  primary: string
}

/** antd token -> the CSS custom property the stylesheet reads it from. */
const TOKEN_VARS: Record<keyof SvgTheme, string> = {
  text: '--ant-color-text',
  textSecondary: '--ant-color-text-secondary',
  border: '--ant-color-border',
  split: '--ant-color-split',
  bgContainer: '--ant-color-bg-container',
  bgElevated: '--ant-color-bg-elevated',
  fillQuaternary: '--ant-color-fill-quaternary',
  fillSecondary: '--ant-color-fill-secondary',
  error: '--ant-color-error',
  primary: '--ant-color-primary',
}

/**
 * Fallbacks for a token that resolves to nothing. Only reachable when the
 * export runs outside the themed tree (tests, a detached node) — the live app
 * always defines these — so they just need to be legible, not exact.
 */
const FALLBACKS: SvgTheme = {
  text: '#000000e0',
  textSecondary: '#000000a6',
  border: '#d9d9d9',
  split: '#0505050f',
  bgContainer: '#ffffff',
  bgElevated: '#ffffff',
  fillQuaternary: '#00000005',
  fillSecondary: '#0000000f',
  error: '#ff4d4f',
  primary: '#1677ff',
}

/**
 * Read every token the renderer needs off `element`'s computed style.
 *
 * `getComputedStyle().getPropertyValue()` returns a custom property's value
 * as authored — antd's are already concrete colors, so no further resolution
 * is needed. A blank result means the var is not in scope; fall back.
 */
export function resolveSvgTheme(element: HTMLElement): SvgTheme {
  const cs = getComputedStyle(element)
  const read = (key: keyof SvgTheme): string =>
    cs.getPropertyValue(TOKEN_VARS[key]).trim() || FALLBACKS[key]

  return {
    text: read('text'),
    textSecondary: read('textSecondary'),
    border: read('border'),
    split: read('split'),
    bgContainer: read('bgContainer'),
    bgElevated: read('bgElevated'),
    fillQuaternary: read('fillQuaternary'),
    fillSecondary: read('fillSecondary'),
    error: read('error'),
    primary: read('primary'),
  }
}

/**
 * The font stack the SVG declares. Read from the live element so the export
 * uses the same faces as the screen; the generic fallbacks matter because the
 * file may be opened on a machine without them.
 */
export function resolveFontFamily(element: HTMLElement): string {
  const live = getComputedStyle(element).fontFamily?.trim()
  if (!live) return GENERIC_STACK

  // The timeline's own CSS sets a system stack, so what comes back here is
  // normally already portable. This guards the case where it is not: a web font
  // (the app shell uses Inter) or one of Next's page-local "<name> Fallback"
  // faces cannot be fetched by a standalone .svg, and a viewer that substitutes
  // one silently changes the metrics the labels were measured against.
  const families = live
    .split(',')
    .map((f) => f.trim())
    .filter((f) => f && !/Fallback/i.test(f))

  return [...families, GENERIC_STACK].join(', ')
}

/** Widely installed faces, ordered so every platform lands on something sane. */
const GENERIC_STACK =
  'system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif'
