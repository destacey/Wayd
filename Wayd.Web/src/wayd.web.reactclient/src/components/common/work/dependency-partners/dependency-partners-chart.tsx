'use client'

import { DependencyHealth } from '@/src/components/types'
import { Flex, theme, Typography } from 'antd'
import {
  DEPENDENCY_HEALTH_VALUES,
  getDependencyHealthChartColor,
} from '../dependency-health-colors'
import {
  DependencyPartner,
  DependencyPartnersData,
  PartnerHealthCounts,
  totalOf,
} from './dependency-partners-data'
import styles from './dependency-partners-chart.module.css'

const { Text } = Typography

export interface DependencyPartnersChartProps {
  data: DependencyPartnersData
  /** The picked partner's key, or null. */
  selection: string | null
  onSelectionChange: (partner: string | null) => void
}

const describe = (counts: PartnerHealthCounts) => {
  const parts = DEPENDENCY_HEALTH_VALUES.filter(
    (v) => counts[v.health] > 0,
  ).map((v) => `${counts[v.health]} ${v.name}`)
  return parts.length > 0 ? ` (${parts.join(', ')})` : ''
}

const rowLabel = (partner: DependencyPartner) =>
  `${partner.name}: we wait on ${totalOf(partner.waitingOn)}${describe(partner.waitingOn)}; they wait on us ${totalOf(partner.waitedOnBy)}${describe(partner.waitedOnBy)}`

/**
 * The other teams a team shares dependencies with, one row each: how many of its dependencies wait on that
 * team, and how many of that team's wait on it, each bar split by health and scaled to the busiest side.
 * Picking a row reports the partner, so the caller can narrow a list to match; picking it again clears it.
 */
const DependencyPartnersChart = ({
  data,
  selection,
  onSelectionChange,
}: DependencyPartnersChartProps) => {
  const { token } = theme.useToken()
  const color = (health: DependencyHealth) =>
    getDependencyHealthChartColor(health, token)

  const max = Math.max(
    1,
    ...data.partners.flatMap((p) => [
      totalOf(p.waitingOn),
      totalOf(p.waitedOnBy),
    ]),
  )

  const bar = (counts: PartnerHealthCounts) => {
    const total = totalOf(counts)
    return (
      <Flex align="center" gap="small" className={styles.cell}>
        <div className={styles.track} aria-hidden>
          <div
            className={styles.bar}
            style={{ width: `${(total / max) * 100}%` }}
          >
            {DEPENDENCY_HEALTH_VALUES.filter((v) => counts[v.health] > 0).map(
              (v) => (
                <span
                  key={v.name}
                  style={{
                    flexGrow: counts[v.health],
                    background: color(v.health),
                  }}
                />
              ),
            )}
          </div>
        </div>
        <Text className={styles.count}>{total}</Text>
      </Flex>
    )
  }

  return (
    <Flex vertical gap="small">
      <div
        className={styles.grid}
        role="group"
        aria-label="Dependencies with other teams"
      >
        <Text type="secondary">Team</Text>
        <Text type="secondary">We wait on them</Text>
        <Text type="secondary">They wait on us</Text>
        {data.partners.map((partner) => {
          const picked = selection === partner.key
          return (
            <button
              key={partner.key}
              type="button"
              className={`${styles.row} ${picked ? styles.picked : ''}`}
              aria-pressed={picked}
              aria-label={rowLabel(partner)}
              onClick={() => onSelectionChange(picked ? null : partner.key)}
            >
              <Text
                strong={picked}
                className={styles.partner}
                title={partner.name}
              >
                {partner.label}
              </Text>
              {bar(partner.waitingOn)}
              {bar(partner.waitedOnBy)}
            </button>
          )
        })}
      </div>
      <Flex wrap gap="middle" component="ul" className={styles.legend}>
        {DEPENDENCY_HEALTH_VALUES.map(({ name, health }) => (
          <Flex key={name} component="li" align="center" gap={6}>
            <span
              className={styles.swatch}
              style={{ background: color(health) }}
              aria-hidden
            />
            <Text type="secondary">{name}</Text>
          </Flex>
        ))}
      </Flex>
    </Flex>
  )
}

export default DependencyPartnersChart
