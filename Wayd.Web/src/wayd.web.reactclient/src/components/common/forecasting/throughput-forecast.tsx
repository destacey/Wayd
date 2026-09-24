'use client'

import { TeamThroughputForecastDto } from '@/src/services/wayd-api'
import { Alert, Flex, Skeleton, Typography } from 'antd'
import { FC } from 'react'
import { ChartCard, METRIC_CARD_FLEX, MetricCard } from '../metrics'
import { ForecastOutcome, formatForecastDate } from './forecast-formatting'
import ForecastWorkItemLink from './forecast-work-item-link'
import ThroughputForecastChart from './throughput-forecast-chart'

const { Text } = Typography

export interface ThroughputForecastProps {
  forecast?: TeamThroughputForecastDto
  isLoading: boolean
  error?: unknown
}

/**
 * A "how many will be done by then?" forecast: the work items a team finishes
 * by the target date at each confidence level, and how far down its backlog
 * that reaches.
 */
const ThroughputForecast: FC<ThroughputForecastProps> = ({
  forecast,
  isLoading,
  error,
}) => {
  if (isLoading) {
    return <Skeleton active />
  }

  if (error || !forecast) {
    return <Alert type="error" title="Unable to load the forecast." showIcon />
  }

  const history = (
    <Text type="secondary">
      {forecast.team.team.name} finished{' '}
      {forecast.team.itemsCompleted.toLocaleString()} backlog work items from{' '}
      {formatForecastDate(forecast.team.from)} to{' '}
      {formatForecastDate(forecast.team.to)}, and has{' '}
      {forecast.backlogWorkItems.toLocaleString()} open backlog work items.
    </Text>
  )

  if (forecast.outcome.id !== ForecastOutcome.Forecast) {
    return (
      <Flex vertical gap="middle">
        <Alert
          type="warning"
          title={forecast.outcome.name}
          description="The team has not finished enough backlog work items recently to forecast from."
          showIcon
        />
        {history}
      </Flex>
    )
  }

  return (
    <Flex vertical gap="middle">
      <Flex gap="small" wrap>
        {forecast.percentiles.map((p) => (
          <MetricCard
            key={p.confidence}
            title={`${p.confidence}% likely`}
            value={`${p.workItems} work items`}
            cardStyle={METRIC_CARD_FLEX}
            tooltip={`${p.confidence}% of simulations finished at least this many work items by ${formatForecastDate(forecast.targetDate)}.`}
            secondaryValue={
              p.throughWorkItem ? (
                <Text type="secondary">
                  through{' '}
                  <ForecastWorkItemLink
                    workItem={p.throughWorkItem}
                    showTitle={false}
                  />
                </Text>
              ) : p.workItems >= forecast.backlogWorkItems &&
                p.workItems > 0 ? (
                <Text type="secondary">the whole backlog</Text>
              ) : undefined
            }
          />
        ))}
      </Flex>

      <ChartCard title="Simulated work items finished">
        <ThroughputForecastChart forecast={forecast} />
        <Text type="secondary">
          {forecast.trials.toLocaleString()} simulations of{' '}
          {forecast.days.toLocaleString()} days,{' '}
          {formatForecastDate(forecast.forecastStart)} to{' '}
          {formatForecastDate(forecast.targetDate)}, from the last{' '}
          {forecast.lookbackDays} days of history. The backlog is ordered{' '}
          {forecast.startedWorkFirst
            ? 'with started work first, then by rank'
            : 'by rank'}
          .
        </Text>
      </ChartCard>

      {history}
    </Flex>
  )
}

export default ThroughputForecast
