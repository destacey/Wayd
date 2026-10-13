import type { GlobalToken } from 'antd'
import {
  WorkDistributionDimension,
  WorkDistributionGroupDto,
  WorkDistributionGroupKind,
  WorkDistributionMeasure,
} from '@/src/services/wayd-api'
import { getLuminance } from '@/src/utils'

/**
 * Each hue twice, a strong shade then a lighter one, so neighbouring groups
 * differ in lightness as well as hue.
 *
 * The dark algorithm reverses antd's palettes — low indexes sit near the
 * background — so dark mode takes its shades from the upper end. Light mode's
 * `blue4` is `#15417e` under the dark algorithm, which vanishes on `#141414`.
 */
const LIGHT_PALETTE = [
  'blue7',
  'blue4',
  'orange8',
  'orange5',
  'cyan8',
  'cyan5',
  'purple7',
  'purple4',
  'gold7',
  'magenta6',
] as const

const DARK_PALETTE = [
  'blue6',
  'blue8',
  'orange6',
  'orange8',
  'cyan6',
  'cyan8',
  'purple7',
  'purple9',
  'gold7',
  'magenta7',
] as const

export type WorkDistributionPaletteTokens = Pick<
  GlobalToken,
  | (typeof LIGHT_PALETTE)[number]
  | (typeof DARK_PALETTE)[number]
  | 'colorBgContainer'
  | 'colorFill'
  | 'colorFillSecondary'
  | 'colorTextQuaternary'
  | 'colorText'
  | 'colorTextLightSolid'
>

export interface GroupSwatch {
  /** A CSS background: a color, or a hatch pattern for work with no project. */
  background: string
  /** Text drawn on the swatch. */
  ink: string
}

const luminance = (color: string): number | undefined => {
  try {
    return getLuminance(color)
  } catch {
    return undefined
  }
}

/** A theme whose container background is not a hex color is taken as light. */
const isDarkTheme = (token: WorkDistributionPaletteTokens) =>
  (luminance(token.colorBgContainer) ?? 1) < 0.5

/**
 * Dark text on a light swatch must stay dark in either theme, so it is the
 * dark theme's background rather than `colorText`, which turns light there.
 */
const inkOn = (color: string, token: WorkDistributionPaletteTokens) => {
  const darkInk = isDarkTheme(token) ? token.colorBgContainer : token.colorText
  return (luminance(color) ?? 1) >= 0.55 ? darkInk : token.colorTextLightSolid
}

/** Hatched so the no-project share reads as a gap rather than as another group. */
export const hatch = (color: string, gap: string) =>
  `repeating-linear-gradient(135deg, ${color} 0 5px, ${gap} 5px 10px)`

/**
 * A swatch per group, by position. Colors follow the groups' order, so every
 * view of one report paints a group the same way.
 */
export const groupSwatches = (
  groups: WorkDistributionGroupDto[],
  token: WorkDistributionPaletteTokens,
): GroupSwatch[] => {
  const palette = isDarkTheme(token) ? DARK_PALETTE : LIGHT_PALETTE
  let next = 0
  return groups.map((group) => {
    if (group.kind === WorkDistributionGroupKind.NoProject)
      return {
        background: hatch(token.colorTextQuaternary, token.colorFillSecondary),
        ink: token.colorText,
      }
    // colorFill rather than colorFillSecondary: at 12% white the lighter fill
    // barely separates from a dark background.
    if (group.kind === WorkDistributionGroupKind.MissingLevel)
      return { background: token.colorFill, ink: token.colorText }

    const color = token[palette[next++ % palette.length]]
    return { background: color, ink: inkOn(color, token) }
  })
}

export const DIMENSION_LABELS: Record<WorkDistributionDimension, string> = {
  [WorkDistributionDimension.Portfolio]: 'Portfolio',
  [WorkDistributionDimension.Program]: 'Program',
  [WorkDistributionDimension.Project]: 'Project',
  [WorkDistributionDimension.StrategicTheme]: 'Strategic theme',
  [WorkDistributionDimension.WorkType]: 'Work type',
}

export const MEASURE_LABELS: Record<WorkDistributionMeasure, string> = {
  [WorkDistributionMeasure.Count]: 'Count',
  [WorkDistributionMeasure.StoryPoints]: 'Story Points',
  [WorkDistributionMeasure.TeamEffort]: 'Share of Team Effort',
}

/** Worded like the Count / Story Points switch on sprint and iteration metrics. */
export const measureTooltip = (isTeamOfTeams: boolean) =>
  isTeamOfTeams
    ? "Switch between counting work items, summing story points, and each team's share of effort. Share of team effort works out each team's split in its own sizing method (story points, effort, size, or work items for teams that size by count, as each sized on the day the work was done), then combines them by the work items each completed, so its percentages never add one team's estimates to another's, or one team's estimates in two units. The story point totals shown alongside are still plain sums across teams."
    : 'Switch between counting work items and summing story points'

export const formatShare = (share: number) =>
  `${share >= 10 || share === 0 ? Math.round(share) : share.toFixed(1)}%`

/** Whole numbers stay whole; a theme split can leave a fraction. */
export const formatAmount = (value: number) =>
  Number.isInteger(value)
    ? value.toLocaleString()
    : Number(value.toFixed(1)).toLocaleString()

const plural = (count: number, word: string) =>
  `${count} ${word}${count === 1 ? '' : 's'}`

const listKeys = (keys: string[], max = 4) =>
  keys.length <= max
    ? keys.join(', ')
    : `${keys.slice(0, max).join(', ')} +${keys.length - max} more`

/** The line under a group's name that says what it holds. */
export const describeGroup = (
  group: WorkDistributionGroupDto,
  dimension: WorkDistributionDimension,
): string | undefined => {
  if (group.kind === WorkDistributionGroupKind.NoProject)
    return 'Neither the item nor its parents link to a project'

  if (group.kind === WorkDistributionGroupKind.MissingLevel)
    return dimension === WorkDistributionDimension.Program
      ? `Projects directly in a portfolio: ${listKeys(group.projectKeys)}`
      : `Projects with no theme: ${listKeys(group.projectKeys)}`

  switch (dimension) {
    case WorkDistributionDimension.Portfolio:
      return [
        group.programCount > 0 && plural(group.programCount, 'program'),
        plural(group.projectKeys.length, 'project'),
      ]
        .filter(Boolean)
        .join(' · ')
    case WorkDistributionDimension.Program:
      return group.portfolio?.name
    case WorkDistributionDimension.Project:
      return [group.portfolio?.name, group.program?.name]
        .filter(Boolean)
        .join(' · ')
    case WorkDistributionDimension.StrategicTheme:
      return listKeys(group.projectKeys)
    default:
      return undefined
  }
}

/** The group's record page, for groups that are a record. */
export const groupHref = (
  group: WorkDistributionGroupDto,
  dimension: WorkDistributionDimension,
): string | undefined => {
  if (group.kind !== WorkDistributionGroupKind.Record || !group.recordKey)
    return undefined

  switch (dimension) {
    case WorkDistributionDimension.Portfolio:
      return `/ppm/portfolios/${group.recordKey}`
    case WorkDistributionDimension.Program:
      return `/ppm/programs/${group.recordKey}`
    case WorkDistributionDimension.Project:
      return `/ppm/projects/${group.recordKey}`
    case WorkDistributionDimension.StrategicTheme:
      return `/strategic-management/strategic-themes/${group.recordKey}`
    default:
      return undefined
  }
}

/** A project group is named with its key, the way projects are listed elsewhere. */
export const groupLabel = (
  group: WorkDistributionGroupDto,
  dimension: WorkDistributionDimension,
) =>
  dimension === WorkDistributionDimension.Project &&
  group.kind === WorkDistributionGroupKind.Record &&
  group.recordKey
    ? `${group.recordKey} · ${group.name}`
    : group.name
