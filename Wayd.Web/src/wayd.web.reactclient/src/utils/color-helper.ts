import { GlobalToken } from 'antd'
import {
  blue,
  cyan,
  gold,
  green,
  magenta,
  orange,
  purple,
  red,
  volcano,
} from '@ant-design/colors'
import { LifecycleCategory } from '../components/types'
import { LifecycleNavigationDto } from '../services/wayd-api'

/**
 * Determines the ant design status color string based on the status of a work status category.
 *
 * @param {string} statusCategory
 * @returns {string} returns an ant design status color string
 */
export const getWorkStatusCategoryColor = (statusCategory: string): string => {
  switch (statusCategory) {
    case 'Active':
      return 'processing'
    case 'Done':
      return 'success'
    case 'Removed':
      return 'error'
    case 'Proposed':
    default:
      return 'default'
  }
}

/**
 * Determines the ant design status color string based on the status of an objective.
 *
 * @param {string} status
 * @returns {string} returns an ant design status color string
 */
export const getObjectiveStatusColor = (status: string): string => {
  switch (status) {
    case 'In Progress':
      return 'processing'
    case 'Completed':
      return 'success'
    case 'Canceled':
    case 'Missed':
      return 'error'
    default:
      return 'default'
  }
}

/**
 * Determines the relative luminance of a given hex color.
 *
 * @param {string} hexColor - The hex color code in the format #RRGGBB.
 * @returns {number} - Returns the relative luminance of the color.
 * @throws {Error} - Throws an error if the hex color format is invalid.
 */
export const getLuminance = (hexColor: string): number => {
  // verify hex color value string format
  if (!/^#[0-9A-F]{6}$/i.test(hexColor)) {
    throw new Error('Invalid hex color format')
  }

  const hex = hexColor.replace('#', '')
  const r = parseInt(hex.slice(0, 2), 16)
  const g = parseInt(hex.slice(2, 4), 16)
  const b = parseInt(hex.slice(4, 6), 16)

  // return relative luminance
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255
}

/**
 * Determines whether a given hex color is considered 'light' or 'dark' based on its luminance.
 *
 * @param {string} hexColor - The hex color code in the format #RRGGBB.
 * @returns {string} - Returns 'light' if the luminance is greater than or equal to 0.5, otherwise 'dark'.
 * @throws {Error} - Throws an error if the hex color format is invalid.
 */
export const getLuminanceTheme = (hexColor: string): string => {
  // returns 'dark' or 'light' based on the luminance of the color
  return getLuminance(hexColor) >= 0.5 ? 'light' : 'dark'
}

/**
 * Resolves a lifecycle category to a concrete theme color, for surfaces that fill with the
 * color itself rather than render a tag — timeline bars.
 *
 * Defined in terms of {@link getLifecycleCategoryTagColor} so a category can never pick up
 * one color as a tag and a different one as a bar. `NotStarted` resolves to grey rather than
 * nothing: returning `undefined` let a bar fall through to the default primary blue, which
 * read as Active.
 */
export const getLifecycleCategoryColor = (
  category: LifecycleCategory,
  token: SemanticColorTokens,
): string => getSemanticChartColor(getLifecycleCategoryTagColor(category), token)

export const getLifecycleCategoryColorFromStatus = (
  status: LifecycleNavigationDto,
  token: SemanticColorTokens,
): string =>
  getLifecycleCategoryColor(
    LifecycleCategory[
      status.lifecycleCategory as keyof typeof LifecycleCategory
    ],
    token,
  )

const avatarColors = [
  '#1677ff', '#722ed1', '#13c2c2', '#eb2f96', '#fa8c16',
  '#52c41a', '#2f54eb', '#faad14', '#f5222d', '#a0d911',
]

/**
 * Returns a deterministic color from a fixed palette based on the given string.
 * Useful for assigning consistent avatar colors to users by ID or name.
 *
 * @param {string} value - A string to hash (e.g., user ID or name).
 * @returns {string} A hex color string from the palette.
 */
export const getAvatarColor = (value: string): string => {
  let hash = 0
  for (let i = 0; i < value.length; i++) {
    hash = (hash * 31 + value.charCodeAt(i)) | 0
  }
  return avatarColors[Math.abs(hash) % avatarColors.length]
}

/**
 * The palette used to distinguish personas on a Story Map. Drawn from the same Ant Design preset
 * hues as the roadmap color picker — one representative (primary, index 5) shade per hue — ordered
 * for visual distinctness so quick-add can assign the next color not already taken.
 */
export const personaColorPalette = [
  purple[5],
  green[5],
  orange[5],
  blue[5],
  magenta[5],
  cyan[5],
  gold[5],
  red[5],
  volcano[5],
]

/**
 * Picks the first palette color not already in use. Comparison is case-insensitive. When every
 * palette color is taken, falls back to cycling the palette by the number of colors used, so a
 * repeat is at least the most-distant reuse rather than always the first color.
 *
 * @param usedColors - The colors already assigned (e.g. existing personas' colors).
 * @returns A hex color from {@link personaColorPalette}.
 */
export const nextUnusedPersonaColor = (usedColors: Iterable<string>): string => {
  const taken = new Set(
    Array.from(usedColors, (c) => c.trim().toLowerCase()),
  )
  const free = personaColorPalette.find((c) => !taken.has(c.toLowerCase()))
  return free ?? personaColorPalette[taken.size % personaColorPalette.length]
}

export const getLifecycleCategoryTagColor = (
  category: LifecycleCategory,
): string => {
  switch (category) {
    case LifecycleCategory.Active:
      return 'processing'
    case LifecycleCategory.Completed:
      return 'success'
    case LifecycleCategory.Canceled:
      return 'error'
    default:
      return 'default'
  }
}

/** The theme tokens a semantic color name can resolve to. */
export type SemanticColorTokens = Pick<
  GlobalToken,
  | 'colorInfo'
  | 'colorSuccess'
  | 'colorError'
  | 'colorWarning'
  | 'colorTextSecondary'
>

export const getSemanticChartColor = (
  semanticColor: string,
  token: SemanticColorTokens,
): string => {
  switch (semanticColor) {
    case 'processing':
      return token.colorInfo
    case 'success':
      return token.colorSuccess
    case 'error':
      return token.colorError
    case 'warning':
      return token.colorWarning
    case 'default':
    default:
      return token.colorTextSecondary
  }
}

/** The theme tokens a status tag's soft-tinted treatment is built from. */
export type StatusSurfaceTokens = Pick<
  GlobalToken,
  | 'colorInfoBg'
  | 'colorInfoBorder'
  | 'colorInfoText'
  | 'colorSuccessBg'
  | 'colorSuccessBorder'
  | 'colorSuccessText'
  | 'colorErrorBg'
  | 'colorErrorBorder'
  | 'colorErrorText'
  | 'colorWarningBg'
  | 'colorWarningBorder'
  | 'colorWarningText'
  | 'colorFillTertiary'
  | 'colorBgContainer'
  | 'colorBorder'
  | 'colorText'
>

export interface StatusSurface {
  background: string
  border: string
  text: string
}

/**
 * Resolves a semantic color name to the same tinted background, border and text an antd
 * Tag uses for it.
 *
 * For surfaces that should read like a status tag rather than a solid block of color — the
 * lit buttons on a filter bar. `getSemanticChartColor` gives the one saturated color a
 * chart mark needs; this gives the three that make up the tag's softer treatment.
 */
export const getSemanticStatusSurface = (
  semanticColor: string,
  token: StatusSurfaceTokens,
): StatusSurface => {
  switch (semanticColor) {
    case 'processing':
      return {
        background: token.colorInfoBg,
        border: token.colorInfoBorder,
        text: token.colorInfoText,
      }
    case 'success':
      return {
        background: token.colorSuccessBg,
        border: token.colorSuccessBorder,
        text: token.colorSuccessText,
      }
    case 'error':
      return {
        background: token.colorErrorBg,
        border: token.colorErrorBorder,
        text: token.colorErrorText,
      }
    case 'warning':
      return {
        background: token.colorWarningBg,
        border: token.colorWarningBorder,
        text: token.colorWarningText,
      }
    case 'default':
    default:
      // Matches antd's own default Tag, which composites colorFillTertiary onto the
      // container to get an opaque fill and uses full-strength text. Reaching for the
      // translucent fill and the muted text instead inverts the treatment in dark mode:
      // a pale film over a dark ground, carrying dimmer text than the colored statuses.
      return {
        background: flattenOnto(token.colorFillTertiary, token.colorBgContainer),
        border: token.colorBorder,
        text: token.colorText,
      }
  }
}

/**
 * The tag treatment for a lifecycle category — the same soft background, border and text
 * the status column shows for it.
 */
export const getLifecycleCategoryStatusSurface = (
  category: LifecycleCategory,
  token: StatusSurfaceTokens,
): StatusSurface =>
  getSemanticStatusSurface(getLifecycleCategoryTagColor(category), token)

const clamp = (value: number, min: number, max: number) =>
  Math.min(Math.max(value, min), max)

interface ParsedColor {
  r: number
  g: number
  b: number
  a: number
}

const parseColor = (color: string): ParsedColor | null => {
  const trimmed = color.trim()

  const hex = trimmed.replace('#', '')
  if (/^[0-9a-fA-F]{6}$/.test(hex)) {
    return {
      r: Number.parseInt(hex.slice(0, 2), 16),
      g: Number.parseInt(hex.slice(2, 4), 16),
      b: Number.parseInt(hex.slice(4, 6), 16),
      a: 1,
    }
  }

  if (/^[0-9a-fA-F]{3}$/.test(hex)) {
    return {
      r: Number.parseInt(hex[0] + hex[0], 16),
      g: Number.parseInt(hex[1] + hex[1], 16),
      b: Number.parseInt(hex[2] + hex[2], 16),
      a: 1,
    }
  }

  if (/^[0-9a-fA-F]{8}$/.test(hex)) {
    return {
      r: Number.parseInt(hex.slice(0, 2), 16),
      g: Number.parseInt(hex.slice(2, 4), 16),
      b: Number.parseInt(hex.slice(4, 6), 16),
      a: Number.parseInt(hex.slice(6, 8), 16) / 255,
    }
  }

  if (/^[0-9a-fA-F]{4}$/.test(hex)) {
    return {
      r: Number.parseInt(hex[0] + hex[0], 16),
      g: Number.parseInt(hex[1] + hex[1], 16),
      b: Number.parseInt(hex[2] + hex[2], 16),
      a: Number.parseInt(hex[3] + hex[3], 16) / 255,
    }
  }

  const rgbMatch = trimmed.match(
    /^rgba?\(\s*(\d{1,3})\s*[, ]\s*(\d{1,3})\s*[, ]\s*(\d{1,3})(?:\s*[,/]\s*(\d*\.?\d+))?\s*\)$/i,
  )
  if (rgbMatch) {
    return {
      r: clamp(Number.parseInt(rgbMatch[1], 10), 0, 255),
      g: clamp(Number.parseInt(rgbMatch[2], 10), 0, 255),
      b: clamp(Number.parseInt(rgbMatch[3], 10), 0, 255),
      a: clamp(
        rgbMatch[4] !== undefined ? Number.parseFloat(rgbMatch[4]) : 1,
        0,
        1,
      ),
    }
  }

  return null
}

const compositeColor = (foreground: ParsedColor, background: ParsedColor) => {
  const a = clamp(foreground.a, 0, 1)
  return {
    r: Math.round(foreground.r * a + background.r * (1 - a)),
    g: Math.round(foreground.g * a + background.g * (1 - a)),
    b: Math.round(foreground.b * a + background.b * (1 - a)),
    a: 1,
  }
}

/**
 * Resolves a translucent color against an opaque one, giving the flat color actually seen.
 *
 * Declared as a function so it hoists above `getSemanticStatusSurface`, which sits earlier in
 * the file than the parsing helpers it relies on.
 */
function flattenOnto(color: string, background: string): string {
  const parsed = parseColor(color)
  const base = parseColor(background)
  if (!parsed || !base) return color

  const flat = compositeColor(parsed, base)
  return `rgb(${flat.r}, ${flat.g}, ${flat.b})`
}

export const softenChartColor = (
  baseColor: string,
  backgroundColor: string,
  softenBy = 0.45,
): string => {
  const base = parseColor(baseColor)
  const background = parseColor(backgroundColor)

  if (!base || !background) return baseColor

  // Resolve semi-transparent colors against the provided background so blending
  // reflects what users actually see on screen.
  const opaqueBackground =
    background.a < 1
      ? compositeColor(background, { r: 255, g: 255, b: 255, a: 1 })
      : background
  const visibleBase = compositeColor(base, opaqueBackground)

  const t = clamp(softenBy, 0, 1)
  const mixed = {
    r: Math.round(visibleBase.r * (1 - t) + opaqueBackground.r * t),
    g: Math.round(visibleBase.g * (1 - t) + opaqueBackground.g * t),
    b: Math.round(visibleBase.b * (1 - t) + opaqueBackground.b * t),
  }

  return `rgb(${mixed.r}, ${mixed.g}, ${mixed.b})`
}
