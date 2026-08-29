import { LifecycleCategory } from '@/src/components/types'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  getLifecycleCategoryTagColor,
  getSemanticChartColor,
} from '@/src/utils'
import type { GlobalToken } from 'antd'
import type { BreakdownDatum } from './breakdown-pie'

/** Records carrying no theme still have to be counted, or the pie lies. */
export const NO_THEME_LABEL = 'No theme'

type ChartTokens = Pick<
  GlobalToken,
  | 'colorInfo'
  | 'colorSuccess'
  | 'colorError'
  | 'colorWarning'
  | 'colorTextSecondary'
>

type HealthChartTokens = Pick<
  GlobalToken,
  'colorSuccess' | 'colorWarning' | 'colorError' | 'colorTextDisabled'
>

/**
 * The shape every PPM list DTO shares that these breakdowns need.
 *
 * Programs, projects and strategic initiatives are separate types with
 * separate status enums — deliberately, since the same number means different
 * things in each. The breakdowns only read the status *name*, so they work
 * across all three without ever comparing ids.
 */
interface StatusBearing {
  status: { name: string; lifecycleCategory: string }
}

interface ThemeBearing {
  strategicThemes: { name: string }[]
}

/**
 * Records grouped by the strategic themes they serve.
 *
 * A record can serve several themes, so the counts deliberately sum to more
 * than the record total — each slice answers "how many touch this theme", not
 * "what share of them". Untagged records are counted under their own label
 * rather than dropped, so the chart accounts for every one in the filtered set.
 */
export const getThemeBreakdown = (
  records: ThemeBearing[],
): BreakdownDatum[] => {
  const counts = new Map<string, number>()

  for (const record of records) {
    if (record.strategicThemes.length === 0) {
      counts.set(NO_THEME_LABEL, (counts.get(NO_THEME_LABEL) ?? 0) + 1)
      continue
    }
    for (const theme of record.strategicThemes) {
      counts.set(theme.name, (counts.get(theme.name) ?? 0) + 1)
    }
  }

  const named = [...counts.entries()]
    .filter(([name]) => name !== NO_THEME_LABEL)
    .sort(([a], [b]) => caseInsensitiveCompare(a, b))
    .map(([type, count]) => ({ type, count }))

  const untagged = counts.get(NO_THEME_LABEL)

  // Last, whatever it sorts to: it is the absence of a theme rather than one
  // of them, and reading it among the named ones invites it being taken for a
  // theme called "No theme".
  return untagged ? [...named, { type: NO_THEME_LABEL, count: untagged }] : named
}

/**
 * Records grouped by status.
 *
 * Slice colors come from the same lifecycle-category mapping the status tags
 * use, resolved through theme tokens, so a status reads the same colour in the
 * chart as on the row beneath it. Grouping is by status *name*: the three PPM
 * status enums reuse the same numbers for different states, so an id-based
 * grouping would be wrong the moment it saw two entity types.
 */
export const getStatusBreakdown = (
  records: StatusBearing[],
  token: ChartTokens,
): BreakdownDatum[] => {
  const counts = new Map<string, { count: number; color: string }>()

  for (const record of records) {
    const name = record.status.name
    const existing = counts.get(name)
    if (existing) {
      existing.count += 1
      continue
    }

    const category =
      LifecycleCategory[
        record.status.lifecycleCategory as keyof typeof LifecycleCategory
      ]
    counts.set(name, {
      count: 1,
      color: getSemanticChartColor(
        getLifecycleCategoryTagColor(category) ?? 'default',
        token,
      ),
    })
  }

  return [...counts.entries()]
    .sort(([a], [b]) => caseInsensitiveCompare(a, b))
    .map(([type, { count, color }]) => ({ type, count, color }))
}

/** Projects with no current health check are still part of the picture. */
export const NO_HEALTH_LABEL = 'No health check'

/**
 * The order health reads in, worst first — a pie sorted alphabetically puts
 * "At Risk" before "Healthy" and buries "Unhealthy" in the middle.
 */
const HEALTH_ORDER = ['Unhealthy', 'At Risk', 'Healthy', NO_HEALTH_LABEL]

interface HealthBearing {
  healthCheck?: { status: { name: string } }
  status: { lifecycleCategory: string }
}

/**
 * Health is a live signal, so it only means anything on work still running.
 * A completed or cancelled project's last check describes a project that no
 * longer exists to be healthy or not, and counting them buries the health of
 * the work actually in flight under the history of everything ever closed.
 */
const isClosed = (lifecycleCategory: string) =>
  lifecycleCategory === 'Completed' || lifecycleCategory === 'Canceled'

/** Names what the health chart leaves out, for its title tooltip. */
export const HEALTH_SCOPE_TOOLTIP =
  'Open projects only. Completed and canceled projects are excluded — their health is no longer current.'

/**
 * Projects grouped by their current health.
 *
 * Colours match the health tags shown on the rows themselves, resolved from
 * tokens rather than the tag helper's CSS variables — the chart library needs
 * a real colour, not a `var()` it cannot resolve.
 *
 * A project that has never been health-checked, or whose check has expired, is
 * counted under its own label rather than dropped: "nobody has reported on
 * this" is the answer most worth seeing on a portfolio.
 *
 * Closed projects are excluded entirely — see `isClosed`.
 */
export const getHealthBreakdown = (
  projects: HealthBearing[],
  token: HealthChartTokens,
): BreakdownDatum[] => {
  const colorFor = (status: string) => {
    switch (status) {
      case 'Healthy':
        return token.colorSuccess
      case 'At Risk':
        return token.colorWarning
      case 'Unhealthy':
        return token.colorError
      default:
        return token.colorTextDisabled
    }
  }

  const counts = new Map<string, number>()

  for (const project of projects) {
    if (isClosed(project.status.lifecycleCategory)) continue
    const name = project.healthCheck?.status.name ?? NO_HEALTH_LABEL
    counts.set(name, (counts.get(name) ?? 0) + 1)
  }

  return [...counts.entries()]
    .sort(([a], [b]) => {
      const ai = HEALTH_ORDER.indexOf(a)
      const bi = HEALTH_ORDER.indexOf(b)
      // An unrecognised status sorts after the known ones rather than to the
      // front, which is where -1 would put it.
      return (
        (ai === -1 ? HEALTH_ORDER.length : ai) -
        (bi === -1 ? HEALTH_ORDER.length : bi)
      )
    })
    .map(([type, count]) => ({ type, count, color: colorFor(type) }))
}
