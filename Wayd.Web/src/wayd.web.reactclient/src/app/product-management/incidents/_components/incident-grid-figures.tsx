'use client'

import EntityLink from '@/src/components/common/entity-link'
import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import { Flex } from 'antd'
import styles from './incidents.module.css'
import { formatIncidentDuration } from './incident-format'

export interface LongestActiveIncident {
  key: number
  title: string
  activeMilliseconds: number
}

export interface IncidentGridFiguresProps {
  activeCount: number
  longestActive?: LongestActiveIncident | null
  meanTimeToRecoverMilliseconds?: number | null
  loading?: boolean
}

const ABSENT = '—'

/**
 * Figures for the incidents matching the list's state and severity filters.
 * They describe that whole set, including rows on other pages.
 */
const IncidentGridFigures = ({
  activeCount,
  longestActive,
  meanTimeToRecoverMilliseconds,
  loading = false,
}: IncidentGridFiguresProps) => (
  <Flex className={styles.figures} gap="small" wrap>
    <MetricCard
      title="Active incidents"
      value={activeCount}
      tooltip="Not closed, not canceled, and not in a done or removed status."
      loading={loading}
      cardStyle={METRIC_CARD_FLEX}
    />
    <MetricCard
      title="Longest active"
      value={longestActive ? formatIncidentDuration(longestActive.activeMilliseconds) : ABSENT}
      secondaryValue={
        longestActive ? (
          <EntityLink href={`/product-management/incidents/${longestActive.key}`}>
            {`INC-${longestActive.key} ${longestActive.title}`}
          </EntityLink>
        ) : undefined
      }
      tooltip="How long the longest active incident has been active, from when it was noticed until now."
      loading={loading}
      cardStyle={METRIC_CARD_FLEX}
    />
    <MetricCard
      title="Mean time to recover"
      value={
        meanTimeToRecoverMilliseconds != null
          ? formatIncidentDuration(meanTimeToRecoverMilliseconds)
          : ABSENT
      }
      tooltip="Average from noticed to recovered for closed incidents. Open and canceled incidents are excluded."
      loading={loading}
      cardStyle={METRIC_CARD_FLEX}
    />
  </Flex>
)

export default IncidentGridFigures
