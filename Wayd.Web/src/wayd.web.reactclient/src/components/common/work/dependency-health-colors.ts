import type { GlobalToken } from 'antd'
import { DependencyHealth } from '../../types'
import { getSemanticChartColor, softenChartColor } from '@/src/utils'
import { getDependencyHealthTagColor } from './dependency-health-tag'

/**
 * The health values a dependency can hold, in severity order.
 *
 * Names must match `DependencyPlanningHealth`'s `[Display]` attributes on the
 * server, which is what `SimpleNavigationDto.Name` carries — hence the space
 * in "At Risk". A mismatch silently drops the value from the scale.
 */
export const DEPENDENCY_HEALTH_VALUES: {
  name: string
  health: DependencyHealth
}[] = [
  { name: 'Healthy', health: DependencyHealth.Healthy },
  { name: 'At Risk', health: DependencyHealth.AtRisk },
  { name: 'Unhealthy', health: DependencyHealth.Unhealthy },
  { name: 'Unknown', health: DependencyHealth.Unknown },
]

type ChartColorTokens = Pick<
  GlobalToken,
  | 'colorInfo'
  | 'colorSuccess'
  | 'colorError'
  | 'colorWarning'
  | 'colorTextSecondary'
  | 'colorBgContainer'
>

/**
 * A G2 `scale.color` pinning each dependency health to the color its tag uses,
 * over the full set of values rather than the ones present.
 *
 * The fixed domain does two things: an absent value cannot re-index the
 * palette and hand Unhealthy the color At Risk had, and the legend keeps every
 * health, so a chart showing only Healthy still names the alternatives.
 */
export const getDependencyHealthColorScale = (token: ChartColorTokens) => ({
  domain: DEPENDENCY_HEALTH_VALUES.map((v) => v.name),
  range: DEPENDENCY_HEALTH_VALUES.map((v) =>
    softenChartColor(
      getSemanticChartColor(getDependencyHealthTagColor(v.health), token),
      token.colorBgContainer,
    ),
  ),
})
