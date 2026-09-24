'use client'

import { WorkItemForecastDto } from '@/src/services/wayd-api'
import { teamUrl } from '@/src/utils'
import { Alert, Card, Flex, Progress, Skeleton, Tag, Typography } from 'antd'
import Link from 'next/link'
import { FC, ReactNode } from 'react'
import { ChartCard, METRIC_CARD_FLEX, MetricCard } from '../metrics'
import CompletionForecastChart from './completion-forecast-chart'
import {
  chanceOfFinishingBy,
  ForecastOutcome,
  formatForecastDate,
  formatPercent,
} from './forecast-formatting'
import ForecastWorkItemLink from './forecast-work-item-link'

const { Text } = Typography

export interface CompletionForecastProps {
  forecast?: WorkItemForecastDto
  isLoading: boolean
  error?: unknown
  /**
   * A date picked in the report, measured against in place of the record's
   * own. The chance is worked out from the histogram, so picking a date does
   * not re-run the forecast.
   */
  targetDateOverride?: Date | string
}

const outcomeAlerts: Record<
  number,
  { type: 'success' | 'info' | 'warning'; description: string }
> = {
  [ForecastOutcome.AlreadyDone]: {
    type: 'success',
    description: 'All of this work is already done.',
  },
  [ForecastOutcome.NotEnoughHistory]: {
    type: 'warning',
    description:
      'The teams involved have not finished enough backlog work items recently to forecast from.',
  },
  [ForecastOutcome.BlockedByDependency]: {
    type: 'warning',
    description:
      'The remaining work waits on a dependency that cannot be forecast. See the issues below.',
  },
  [ForecastOutcome.CannotForecast]: {
    type: 'warning',
    description:
      'None of the remaining work can be forecast. See the issues below.',
  },
  [ForecastOutcome.NoRemainingWork]: {
    type: 'info',
    description: 'There are no open backlog work items to forecast.',
  },
}

const Section: FC<{ title: string; children: ReactNode }> = ({
  title,
  children,
}) => (
  <Card size="small" title={title}>
    <Flex vertical gap={8}>
      {children}
    </Flex>
  </Card>
)

/**
 * A "when will it be done?" forecast: the date the remaining work finishes at
 * each confidence level, the chance of making a target date, the spread of
 * simulated finish dates, and anything that limited the forecast.
 */
const CompletionForecast: FC<CompletionForecastProps> = ({
  forecast,
  isLoading,
  error,
  targetDateOverride,
}) => {
  if (isLoading) {
    return <Skeleton active />
  }

  if (error || !forecast) {
    return <Alert type="error" title="Unable to load the forecast." showIcon />
  }

  const isForecast = forecast.outcome.id === ForecastOutcome.Forecast
  const outcomeAlert = outcomeAlerts[forecast.outcome.id]

  const targetDate = targetDateOverride ?? forecast.targetDate
  const chance = targetDate
    ? chanceOfFinishingBy(forecast.histogram, forecast.trials, targetDate)
    : undefined

  const dependencies = [...forecast.dependencies].sort(
    (a, b) =>
      (b.shareOfTrialsSettingFinish ?? -1) -
      (a.shareOfTrialsSettingFinish ?? -1),
  )

  return (
    <Flex vertical gap="middle">
      {forecast.ignoreDependencies && (
        <Alert
          type="info"
          title="Dependencies ignored"
          description="A what-if: this forecast does not wait on any predecessors."
          showIcon
        />
      )}
      {outcomeAlert && (
        <Alert
          type={outcomeAlert.type}
          title={forecast.outcome.name}
          description={outcomeAlert.description}
          showIcon
        />
      )}

      {isForecast && (
        <>
          <Flex gap="small" wrap>
            {forecast.percentiles.map((p) => (
              <MetricCard
                key={p.confidence}
                title={`${p.confidence}% likely by`}
                value={p.date ? formatForecastDate(p.date) : 'Beyond 2 years'}
                cardStyle={METRIC_CARD_FLEX}
                tooltip={`${p.confidence}% of simulations finished on or before this date.`}
              />
            ))}
            {targetDate && chance !== undefined && (
              <MetricCard
                title={`Chance by ${formatForecastDate(targetDate)}`}
                value={formatPercent(chance)}
                cardStyle={METRIC_CARD_FLEX}
                tooltip="The share of simulations that finished on or before the target date."
              />
            )}
          </Flex>

          <ChartCard title="Simulated completion dates">
            <CompletionForecastChart
              forecast={forecast}
              targetDate={targetDate}
            />
            <Text type="secondary">
              {forecast.trials.toLocaleString()} simulations from the last{' '}
              {forecast.lookbackDays} days of history, starting{' '}
              {formatForecastDate(forecast.forecastStart)}
              {targetDate && '; lighter bars finish after the target date'}
              {forecast.trialsBeyondHorizon > 0 &&
                `; ${forecast.trialsBeyondHorizon.toLocaleString()} did not finish within 2 years`}
              .
            </Text>
          </ChartCard>
        </>
      )}

      {forecast.remainingWorkItems > 0 && (
        <Section title="Based on">
          <Text>
            {forecast.remainingWorkItems.toLocaleString()} open backlog work{' '}
            {forecast.remainingWorkItems === 1 ? 'item' : 'items'}
            {forecast.backlogPosition != null &&
              `, at position ${forecast.backlogPosition} in its team's backlog`}
            . Backlogs are ordered{' '}
            {forecast.startedWorkFirst
              ? 'with started work first, then by rank'
              : 'by rank'}
            .
          </Text>
          {forecast.teams.map((t) => (
            <Text key={t.team.id}>
              <Link href={teamUrl(t.team)}>{t.team.name}</Link>
              <Text type="secondary">
                {' '}
                finished {t.itemsCompleted.toLocaleString()} backlog work items
                from {formatForecastDate(t.from)} to {formatForecastDate(t.to)}
              </Text>
            </Text>
          ))}
        </Section>
      )}

      {forecast.issues.length > 0 && (
        <Section title="Issues">
          {forecast.issues.map((issue) => (
            <Flex key={`${issue.workItem.id}-${issue.type.id}`} gap="small">
              <Tag>{issue.type.name}</Tag>
              <ForecastWorkItemLink workItem={issue.workItem} />
            </Flex>
          ))}
        </Section>
      )}

      {forecast.excludedWorkItems.length > 0 && (
        <Section title="Left out of the forecast">
          <Text type="secondary">
            These could not be forecast, so the dates above are a lower bound.
          </Text>
          {forecast.excludedWorkItems.map((w) => (
            <ForecastWorkItemLink key={w.id} workItem={w} />
          ))}
        </Section>
      )}

      {dependencies.length > 0 && (
        <Section title="Dependencies">
          <Text type="secondary">
            How often waiting on each predecessor set when its successor
            finished.
          </Text>
          {dependencies.map((d) => (
            <Flex
              key={`${d.predecessor.id}-${d.successor.id}`}
              gap="small"
              align="center"
              wrap
            >
              <ForecastWorkItemLink
                workItem={d.predecessor}
                showTitle={false}
              />
              <Text type="secondary">blocks</Text>
              <ForecastWorkItemLink workItem={d.successor} showTitle={false} />
              {d.shareOfTrialsSettingFinish !== undefined ? (
                <Progress
                  percent={Math.round(d.shareOfTrialsSettingFinish * 100)}
                  size="small"
                  style={{ width: 160, marginBottom: 0 }}
                />
              ) : (
                <Text type="secondary">not forecast</Text>
              )}
            </Flex>
          ))}
        </Section>
      )}

      {forecast.ignoredDependencies.length > 0 && (
        <Section title="Ignored dependencies">
          <Text type="secondary">
            These dependencies were left out of the forecast and may need
            cleaning up.
          </Text>
          {forecast.ignoredDependencies.map((d) => (
            <Flex
              key={`${d.predecessor.id}-${d.successor.id}`}
              gap="small"
              align="center"
              wrap
            >
              <Tag>{d.reason.name}</Tag>
              <ForecastWorkItemLink
                workItem={d.predecessor}
                showTitle={false}
              />
              <Text type="secondary">blocks</Text>
              <ForecastWorkItemLink workItem={d.successor} showTitle={false} />
            </Flex>
          ))}
        </Section>
      )}
    </Flex>
  )
}

export default CompletionForecast
