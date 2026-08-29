'use client'

import {
  WorkItemDetailsDto,
  WorkItemListDto,
  WorkItemProgressDailyRollupDto,
} from '@/src/services/wayd-api'
import BreakdownPie from '@/src/app/ppm/_components/breakdown-pie'
import {
  METRIC_CARD_FLEX,
  MetricCard,
} from '@/src/components/common/metrics'
import { WorkItemsCumulativeFlowChart } from '@/src/components/common/work'
import { getDependencyHealthColorScale } from '@/src/components/common/work/dependency-health-colors'
import {
  useGetWorkItemDependenciesQuery,
  useGetWorkItemMetricsQuery,
} from '@/src/store/features/work-management/workspace-api'
import { WorkTypeTier } from '@/src/components/types'
import { Col, Flex, Row, theme } from 'antd'
import WorkItemCycleTime from '../work-item-cycle-time'
import WorkItemLeadTime from '../work-item-lead-time'
import WorkItemProgressPieChart from '../work-item-progress-pie-chart'
import WorkItemSteps from '../work-item-steps'
import WorkItemTimeToStart from '../work-item-time-to-start'
import {
  DEPENDENCY_HEALTH_TOOLTIP,
  getChildTypeBreakdown,
  getCrossTeamDependencies,
  getDependencyHealthBreakdown,
} from './work-item-breakdowns'

export interface WorkItemOverviewProps {
  workItem: WorkItemDetailsDto
  /**
   * The direct children, from the same query the Child Work Items section
   * renders — so a count here cannot disagree with the list it links to.
   */
  childWorkItems: WorkItemListDto[]
  childWorkItemsLoading: boolean
  /** Section ids to link the tiles to, supplied so they stay defined once. */
  sectionIds: { workItems: string; dependencies: string }
  /** Navigates to a section by id, so ids stay defined on the page. */
  onNavigateToSection: (sectionId: string) => void
}

/**
 * How the work item is tracking: its lifecycle position, timings, and
 * breakdowns of what it holds. Its attributes live in the facts panel.
 *
 * Timings render for every work item; the rollup charts and the child
 * breakdown are portfolio-tier only, since neither has meaning for a single
 * requirement. A removed item still shows all of it — hiding the section for
 * one status would make the page's shape depend on which item you opened.
 */
const WorkItemOverview = ({
  workItem,
  childWorkItems,
  childWorkItemsLoading,
  sectionIds,
  onNavigateToSection,
}: WorkItemOverviewProps) => {
  const { token } = theme.useToken()
  const isPortfolioTier = workItem.type.tier.id === WorkTypeTier.Portfolio

  const { data: metricsData, isLoading } = useGetWorkItemMetricsQuery(
    {
      idOrKey: workItem.workspace.key,
      workItemKey: workItem.key,
    },
    { skip: !isPortfolioTier },
  )

  // Same skip mechanic as the rollup: the Dependencies section fetches this
  // itself when opened, and RTK Query serves both from one cache entry.
  const { data: dependencyData, isLoading: dependenciesLoading } =
    useGetWorkItemDependenciesQuery(
      {
        workspaceIdOrKey: workItem.workspace.key,
        workItemKey: workItem.key,
      },
      { skip: !workItem },
    )

  // The rollup is a daily series; its last entry is the current standing.
  const progress: WorkItemProgressDailyRollupDto | null =
    metricsData && metricsData.length > 0
      ? metricsData[metricsData.length - 1]
      : null

  const childTypes = isPortfolioTier
    ? getChildTypeBreakdown(childWorkItems)
    : []
  const crossTeamDependencies = getCrossTeamDependencies(dependencyData ?? [])
  const dependencyHealth = getDependencyHealthBreakdown(dependencyData ?? [])

  // Shown while loading so the card mounts and renders its skeleton; gating on
  // the data alone means the chart appears only once it arrives, shifting the
  // row under the reader.
  const showChildTypes =
    isPortfolioTier && (childWorkItemsLoading || childTypes.length > 0)
  const showDependencyHealth =
    dependenciesLoading || dependencyHealth.length > 0
  const showProgress = isPortfolioTier && (isLoading || !!progress)

  const charts = [
    showChildTypes && {
      id: 'child-types',
      chart: (
        <BreakdownPie
          title="Child Work Items by Type"
          data={childTypes}
          isLoading={childWorkItemsLoading}
        />
      ),
    },
    showProgress && {
      id: 'progress',
      chart: (
        <WorkItemProgressPieChart progress={progress} isLoading={isLoading} />
      ),
    },
    showDependencyHealth && {
      id: 'dependency-health',
      chart: (
        <BreakdownPie
          title="Cross-Team Dependencies by Health"
          data={dependencyHealth}
          isLoading={dependenciesLoading}
          tooltip={DEPENDENCY_HEALTH_TOOLTIP}
          colorScale={getDependencyHealthColorScale(token)}
        />
      ),
    },
  ].filter((c) => c !== null && c !== false)

  const chartSpanLg = charts.length === 1 ? 24 : 12
  const chartSpanXl = charts.length === 3 ? 8 : chartSpanLg

  return (
    <Flex vertical gap="middle">
      <WorkItemSteps workItem={workItem} />

      {/* A wrapping flex row rather than a Col grid: METRIC_CARD_FLEX divides
          the row evenly and wraps, so a work item showing one timing does not
          leave empty columns beside it. */}
      <Flex gap="middle" wrap align="stretch">
        <WorkItemTimeToStart workItem={workItem} />
        <WorkItemCycleTime workItem={workItem} />
        <WorkItemLeadTime workItem={workItem} />

        {/* A tile is hidden only once its query has resolved and found nothing.
            Hiding on a zero count alone makes tiles pop in as data lands, and
            renders "none" identically to "not loaded yet". */}
        {isPortfolioTier &&
          (childWorkItemsLoading || childWorkItems.length > 0) && (
            <MetricCard
              title="Child Work Items"
              value={childWorkItems.length}
              loading={childWorkItemsLoading}
              cardStyle={METRIC_CARD_FLEX}
              onClick={() => onNavigateToSection(sectionIds.workItems)}
            />
          )}
        {(dependenciesLoading || crossTeamDependencies.length > 0) && (
          <MetricCard
            title="Cross-Team Dependencies"
            value={crossTeamDependencies.length}
            loading={dependenciesLoading}
            tooltip={DEPENDENCY_HEALTH_TOOLTIP}
            cardStyle={METRIC_CARD_FLEX}
            onClick={() => onNavigateToSection(sectionIds.dependencies)}
          />
        )}
      </Flex>

      {charts.length > 0 && (
        <Row gutter={[16, 16]} align="stretch">
          {charts.map(({ id, chart }) => (
            <Col key={id} xs={24} lg={chartSpanLg} xl={chartSpanXl}>
              {chart}
            </Col>
          ))}
        </Row>
      )}

      {/* Its own row at full width: a cumulative flow is a wide time series,
          and halving it compresses the x-axis into unreadability. */}
      {isPortfolioTier && metricsData && (
        <WorkItemsCumulativeFlowChart
          workItems={metricsData}
          isLoading={isLoading}
        />
      )}
    </Flex>
  )
}

export default WorkItemOverview
