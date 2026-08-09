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

export const getLifecycleCategoryColor = (
  category: LifecycleCategory,
  token: GlobalToken, // GlobalToken from antd theme
): string | undefined => {
  switch (category) {
    case LifecycleCategory.Active:
      return token.colorPrimary // or token.colorPrimary
    case LifecycleCategory.Completed:
      return token.colorSuccess
    default:
      return undefined
  }
}

export const getLifecycleCategoryColorFromStatus = (
  status: LifecycleNavigationDto,
  token: any,
): string | undefined => {
  // Map string to enum
  const category =
    LifecycleCategory[status.lifecycleCategory as keyof typeof LifecycleCategory]
  return getLifecycleCategoryColor(category, token)
}

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
): string | undefined => {
  switch (category) {
    case LifecycleCategory.Active:
      return 'processing'
    case LifecycleCategory.Completed:
      return 'success'
    default:
      return 'default'
  }
}

export const getSemanticChartColor = (
  semanticColor: string,
  token: Pick<
    GlobalToken,
    | 'colorInfo'
    | 'colorSuccess'
    | 'colorError'
    | 'colorWarning'
    | 'colorTextSecondary'
  >,
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
