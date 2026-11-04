'use client'

import { DependencyHealth } from '@/src/components/types'
import { getSemanticChartColor } from '@/src/utils'
import { Flex, theme, Typography } from 'antd'
import { DEPENDENCY_HEALTH_VALUES } from '../work/dependency-health-colors'
import { getDependencyHealthTagColor } from '../work/dependency-health-tag'
import {
  DependencyHealthCounts,
  DependencyWheelFlow,
  DependencyWheelGroup,
} from './dependency-wheel-data'
import styles from './dependency-wheel.module.css'

const { Text } = Typography

const MAX_PARTNERS = 3

export type DependencyWheelTooltipSubject =
  | { kind: 'group'; group: DependencyWheelGroup; color: string }
  | {
      kind: 'flow'
      flow: DependencyWheelFlow
      source: DependencyWheelGroup
      target: DependencyWheelGroup
      sourceColor: string
      targetColor: string
    }

const plural = (count: number) =>
  `${count} ${count === 1 ? 'dependency' : 'dependencies'}`

const Swatch = ({ color }: { color: string }) => (
  <span className={styles.swatch} style={{ background: color }} aria-hidden />
)

/** A bar split by health, with the count of each health present beneath it. */
const HealthBreakdown = ({ counts }: { counts: DependencyHealthCounts }) => {
  const { token } = theme.useToken()
  const total = DEPENDENCY_HEALTH_VALUES.reduce(
    (sum, v) => sum + counts[v.health],
    0,
  )
  const present = DEPENDENCY_HEALTH_VALUES.filter((v) => counts[v.health] > 0)
  const color = (health: DependencyHealth) =>
    getSemanticChartColor(getDependencyHealthTagColor(health), token)

  return (
    <Flex vertical gap={4}>
      <div className={styles.healthBar} aria-hidden>
        {present.map((v) => (
          <span
            key={v.name}
            style={{
              width: `${(counts[v.health] / total) * 100}%`,
              background: color(v.health),
            }}
          />
        ))}
      </div>
      <Flex wrap gap="small">
        {present.map((v) => (
          <Flex key={v.name} align="center" gap={4}>
            <Swatch color={color(v.health)} />
            <Text>
              {counts[v.health]} {v.name}
            </Text>
          </Flex>
        ))}
      </Flex>
    </Flex>
  )
}

const Row = ({ label, value }: { label: string; value: number }) => (
  <Flex justify="space-between" gap="large">
    <Text type="secondary">{label}</Text>
    <Text strong>{value}</Text>
  </Flex>
)

/** The summary shown while a team or a ribbon on the dependency wheel is hovered or focused. */
const DependencyWheelTooltip = ({
  subject,
}: {
  subject: DependencyWheelTooltipSubject
}) => {
  if (subject.kind === 'group') {
    const { group, color } = subject
    const partners = group.partners.slice(0, MAX_PARTNERS)
    return (
      <Flex vertical gap="small">
        <Flex align="center" justify="space-between" gap="middle">
          <Flex align="center" gap={6}>
            <Swatch color={color} />
            <Text strong>{group.name}</Text>
          </Flex>
          <Text type="secondary">{group.label}</Text>
        </Flex>
        <Flex vertical>
          <Row label="Waiting on others" value={group.incoming} />
          <Row label="Others waiting on it" value={group.outgoing} />
        </Flex>
        <HealthBreakdown counts={group.healthCounts} />
        {partners.length > 0 && (
          <Text type="secondary">
            Most linked:{' '}
            {partners.map((p) => `${p.label} ${p.count}`).join(' · ')}
          </Text>
        )}
        <Text type="secondary">Click to filter the list</Text>
      </Flex>
    )
  }

  const { flow, source, target, sourceColor, targetColor } = subject
  return (
    <Flex vertical gap="small">
      <Flex align="center" gap={6}>
        <Swatch color={sourceColor} />
        <Text strong>{source.label}</Text>
        <Text type="secondary">→</Text>
        <Swatch color={targetColor} />
        <Text strong>{target.label}</Text>
      </Flex>
      <Text type="secondary">
        {plural(flow.count)} {target.label} waits on
      </Text>
      <HealthBreakdown counts={flow.healthCounts} />
      <Text type="secondary">
        Open {flow.open} · Done {flow.done}
      </Text>
      <Text type="secondary">Click to filter the list</Text>
    </Flex>
  )
}

export default DependencyWheelTooltip
