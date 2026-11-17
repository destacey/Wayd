'use client'

import {
  KpiHealth,
  KpiTargetDirection,
  KpiTrend,
} from '@/src/services/api/wayd.schemas'
import useTheme from '@/src/components/contexts/theme'
import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  ExclamationCircleOutlined,
  InfoCircleOutlined,
  PlusCircleOutlined,
} from '@ant-design/icons'
import { Card, Flex, Progress, Skeleton, Tag, Typography } from 'antd'
import { GlobalToken } from 'antd'
import dayjs from 'dayjs'
import { CSSProperties, FC } from 'react'
import styles from './kpi-card.module.css'

const { Text } = Typography
const { Input: SkeletonInput } = Skeleton

// ─── Interfaces ───────────────────────────────────────────────────────────────

export interface KpiCardData {
  id: string
  key: number
  name: string
  startingValue?: number | undefined | null
  targetValue: number
  actualValue?: number | undefined | null
  prefix?: string | undefined | null
  suffix?: string | undefined | null
  targetDirection: KpiTargetDirection
  progress?: number | undefined | null
  health?: KpiHealth | undefined
  trend?: KpiTrend | undefined
}

export interface KpiCardCheckpoint {
  label: string
  date: string
  targetValue: number
  actualValue?: number | undefined
  health?: KpiHealth | undefined | null
  trend?: KpiTrend | undefined | null
}

export interface KpiCardProps {
  data: KpiCardData
  checkpoints?: KpiCardCheckpoint[]
  checkpointLoading?: boolean
  onPress?: (id: string) => void
}

// ─── CSS custom properties ────────────────────────────────────────────────────
// Dynamic token-derived colors are set as CSS vars on the Card root.
// Sub-components reference them via var(--kpi-*) in kpi-card.module.css.

interface KpiCssVars extends CSSProperties {
  '--kpi-accent': string
  '--kpi-health-text': string
  '--kpi-actual-color': string
  '--kpi-trend-color': string
  '--kpi-muted': string
  '--kpi-secondary': string
  '--kpi-error': string
  '--kpi-error-bg': string
  '--kpi-error-border': string
  '--kpi-primary': string
  '--kpi-border': string
  '--kpi-fill-quaternary': string
  '--kpi-checkpoint-border': string
}

// ─── Color helpers ────────────────────────────────────────────────────────────

function getHealthTokens(
  health: KpiHealth | undefined | null,
  token: GlobalToken,
) {
  if (health === KpiHealth.Healthy)
    return {
      text: token.colorSuccess,
      bg: token.colorSuccessBg,
      border: token.colorSuccessBorder,
      tagColor: 'success' as const,
    }
  if (health === KpiHealth.AtRisk)
    return {
      text: token.colorWarning,
      bg: token.colorWarningBg,
      border: token.colorWarningBorder,
      tagColor: 'warning' as const,
    }
  if (health === KpiHealth.Unhealthy)
    return {
      text: token.colorError,
      bg: token.colorErrorBg,
      border: token.colorErrorBorder,
      tagColor: 'error' as const,
    }
  return {
    text: token.colorTextTertiary,
    bg: token.colorFillQuaternary,
    border: token.colorBorderSecondary,
    tagColor: 'default' as const,
  }
}

function getTrendColor(trend: KpiTrend, token: GlobalToken): string {
  if (trend === KpiTrend.Improving) return token.colorSuccess
  if (trend === KpiTrend.Worsening) return token.colorError
  return token.colorTextSecondary
}

function getTrendArrow(trend: KpiTrend, direction: KpiTargetDirection): string {
  if (trend === KpiTrend.Improving)
    return direction === KpiTargetDirection.Increase ? '↑' : '↓'
  if (trend === KpiTrend.Worsening)
    return direction === KpiTargetDirection.Increase ? '↓' : '↑'
  if (trend === KpiTrend.Stable) return '→'
  return ''
}

function isRegressed(data: KpiCardData): boolean {
  if (data.startingValue == null || data.actualValue == null) return false
  return data.targetDirection === KpiTargetDirection.Increase
    ? data.actualValue < data.startingValue
    : data.actualValue > data.startingValue
}

function formatValue(
  value: number,
  prefix?: string | null,
  suffix?: string | null,
): string {
  return `${prefix ?? ''}${value.toLocaleString()}${suffix ?? ''}`
}

// ─── Progress state ───────────────────────────────────────────────────────────

type ProgressState =
  | { type: 'normal'; pct: number }
  | { type: 'slight'; pct: number }
  | { type: 'severe'; pct: number }
  | { type: 'noBaseline' }
  | { type: 'noData' }

function getProgressState(data: KpiCardData): ProgressState {
  if (data.actualValue == null) return { type: 'noData' }
  if (data.progress == null) return { type: 'noBaseline' }
  if (data.progress >= 0) return { type: 'normal', pct: data.progress }
  if (data.progress >= -0.15) return { type: 'slight', pct: data.progress }
  return { type: 'severe', pct: data.progress }
}

// ─── Sub-components ───────────────────────────────────────────────────────────

const DirectionBadge: FC<{ direction: KpiTargetDirection }> = ({
  direction,
}) => (
  <Tag
    icon={
      direction === KpiTargetDirection.Increase ? (
        <ArrowUpOutlined />
      ) : (
        <ArrowDownOutlined />
      )
    }
    className={styles.directionTag}
  >
    {direction}
  </Tag>
)

const HealthBadge: FC<{
  health: KpiHealth | undefined | null
  token: GlobalToken
}> = ({ health, token }) => {
  if (health == null) return null

  const { tagColor } = getHealthTokens(health, token)
  const label =
    health === KpiHealth.Healthy
      ? 'Healthy'
      : health === KpiHealth.AtRisk
        ? 'At Risk'
        : 'Unhealthy'
  return (
    <Tag
      aria-label={`Health: ${label}`}
      color={tagColor}
      className={styles.healthTag}
    >
      {label}
    </Tag>
  )
}

const ProgressSection: FC<{ data: KpiCardData; healthColor: string }> = ({
  data,
  healthColor,
}) => {
  const state = getProgressState(data)

  if (state.type === 'noData') {
    return (
      <Flex align="center" gap={6} className={styles.hint}>
        <InfoCircleOutlined className={styles.hintIcon} />
        <Text className={styles.hintText}>Awaiting first measurement</Text>
      </Flex>
    )
  }

  if (state.type === 'noBaseline') {
    return (
      <Flex align="center" gap={6} className={styles.hint}>
        <InfoCircleOutlined className={styles.hintIcon} />
        <Text className={styles.hintText}>
          No baseline · progress unavailable
        </Text>
      </Flex>
    )
  }

  if (state.type === 'severe') {
    const pctDisplay = Math.round(state.pct * 100)
    const startStr =
      data.startingValue != null
        ? formatValue(data.startingValue, data.prefix, data.suffix)
        : '—'
    const actualStr =
      data.actualValue != null
        ? formatValue(data.actualValue, data.prefix, data.suffix)
        : '—'
    return (
      <Flex align="center" gap={6} className={styles.regression}>
        <ExclamationCircleOutlined className={styles.regressionIcon} />
        <Text className={styles.regressionText}>
          Regressed · {actualStr} vs baseline {startStr} ({pctDisplay}%)
        </Text>
      </Flex>
    )
  }

  const pctDisplay = Math.round(state.pct * 100)
  const startStr =
    data.startingValue != null
      ? formatValue(data.startingValue, data.prefix, data.suffix)
      : '—'

  if (state.type === 'slight') {
    // Notch bar — no antd Progress equivalent for below-baseline state
    return (
      <Flex vertical gap={4}>
        <div className={styles.progressTrack}>
          <div className={styles.progressNotch} />
        </div>
        <Flex justify="space-between" align="center">
          <Text
            className={`${styles.progressLabel} ${styles.progressLabelMuted}`}
          >
            Baseline: {startStr}
          </Text>
          <Text
            className={`${styles.progressLabel} ${styles.progressLabelError}`}
          >
            {pctDisplay}% · Below baseline
          </Text>
        </Flex>
      </Flex>
    )
  }

  return (
    <Flex vertical gap={4}>
      <Progress
        percent={Math.min(Math.round(state.pct * 100), 100)}
        showInfo={false}
        size="small"
        strokeColor={healthColor}
        style={{ margin: 0 }}
      />
      <Flex justify="space-between" align="center">
        <Text
          className={`${styles.progressLabel} ${styles.progressLabelMuted}`}
        >
          Baseline: {startStr}
        </Text>
        <Text
          className={`${styles.progressLabel} ${styles.progressLabelHealth}`}
        >
          {pctDisplay}%
        </Text>
      </Flex>
    </Flex>
  )
}

const CheckpointStrip: FC<{
  checkpoint: KpiCardCheckpoint
  prefix?: string | null
  suffix?: string | null
  token: GlobalToken
}> = ({ checkpoint, prefix, suffix, token }) => {
  const hasActual = checkpoint.actualValue != null
  const dateStr = dayjs(checkpoint.date).format('MMM D')

  const { text: resultColor } = getHealthTokens(checkpoint.health, token)

  let resultText: string
  if (!hasActual) {
    resultText = 'Upcoming'
  } else if (checkpoint.health === KpiHealth.Healthy) {
    resultText = `✓ Met (${formatValue(checkpoint.actualValue!, prefix, suffix)})`
  } else if (checkpoint.health === KpiHealth.AtRisk) {
    resultText = `! Missed (${formatValue(checkpoint.actualValue!, prefix, suffix)} / ${formatValue(checkpoint.targetValue, prefix, suffix)})`
  } else {
    resultText = `✕ Off Track (${formatValue(checkpoint.actualValue!, prefix, suffix)} / ${formatValue(checkpoint.targetValue, prefix, suffix)})`
  }

  return (
    <Flex
      justify="space-between"
      align="center"
      gap={8}
      className={`${styles.checkpoint} ${hasActual ? styles.checkpointMeasured : styles.checkpointUpcoming}`}
    >
      <Text className={styles.checkpointMeta}>
        <Text strong className={styles.checkpointLabel}>
          {checkpoint.label}
        </Text>
        {' · '}
        {dateStr} · Target:{' '}
        {formatValue(checkpoint.targetValue, prefix, suffix)}
      </Text>
      <Text className={styles.checkpointResult} style={{ color: resultColor }}>
        {resultText}
      </Text>
    </Flex>
  )
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function getTrendLabel(trend: KpiTrend): string {
  if (trend === KpiTrend.Improving) return 'Improving'
  if (trend === KpiTrend.Worsening) return 'Worsening'
  return 'Stable'
}

const cardBodyStyles: CSSProperties = {
  padding: 0,
  display: 'flex',
  flexDirection: 'column',
  height: '100%',
}

// ─── Main Component ───────────────────────────────────────────────────────────

const KpiCard: FC<KpiCardProps> = ({
  data,
  checkpoints,
  checkpointLoading,
  onPress,
}) => {
  const { token } = useTheme()

  // Derive checkpoint and nextCheckpoint from the full list.
  // checkpoint    — most recent past checkpoint that has a measurement
  // nextCheckpoint — first future checkpoint, or first past checkpoint with no measurement
  const { checkpoint, nextCheckpoint } = (() => {
    const now = dayjs()
    const sorted = [...(checkpoints ?? [])].sort(
      (a, b) => dayjs(a.date).valueOf() - dayjs(b.date).valueOf(),
    )
    const past = sorted.filter(
      (cp) => dayjs(cp.date).isBefore(now) && cp.actualValue != null,
    )
    const checkpoint = past.length > 0 ? past[past.length - 1] : undefined
    const next = sorted.find(
      (cp) => !dayjs(cp.date).isBefore(now) || cp.actualValue == null,
    )
    return { checkpoint, nextCheckpoint: next ?? undefined }
  })()

  // Card-level health drives: accent bar, health badge, and actual value color.
  // The actual value color always matches the health badge color (unless regressed).
  // Health source priority:
  //   1. data.health (from KPI list DTO, rarely populated)
  //   2. nextCheckpoint — if it already has a measurement
  //   3. checkpoint (last measured) — fallback when nextCheckpoint has no measurement
  // This keeps the card-level health and the checkpoint strip independent
  // when newer measurements exist on the next checkpoint.
  const { effectiveHealth, effectiveTrend, health, trendArrow, hasActual, cssVars } = (() => {
    const nextHasMeasurement = nextCheckpoint?.actualValue != null
    const effectiveHealth =
      data.health ??
      (nextHasMeasurement ? nextCheckpoint?.health : undefined) ??
      checkpoint?.health
    const effectiveTrend =
      data.trend ??
      (nextHasMeasurement ? nextCheckpoint?.trend : undefined) ??
      checkpoint?.trend
    const health = getHealthTokens(effectiveHealth, token)
    const regressed = isRegressed(data)
    const actualColor = regressed ? token.colorError : health.text
    const hasActual = data.actualValue != null
    const hasTrend = effectiveTrend && effectiveTrend !== KpiTrend.NoData
    const trendColor = hasTrend
      ? getTrendColor(effectiveTrend, token)
      : token.colorTextSecondary
    const trendArrow = hasTrend
      ? getTrendArrow(effectiveTrend, data.targetDirection)
      : null
    const checkpointBorderColor = checkpoint
      ? checkpoint.actualValue != null
        ? getHealthTokens(checkpoint.health, token).text
        : token.colorTextTertiary
      : token.colorTextTertiary

    const cssVars: KpiCssVars = {
      '--kpi-accent': health.text,
      '--kpi-health-text': health.text,
      '--kpi-actual-color': hasActual ? actualColor : token.colorTextTertiary,
      '--kpi-trend-color': trendColor,
      '--kpi-muted': token.colorTextTertiary,
      '--kpi-secondary': token.colorTextSecondary,
      '--kpi-error': token.colorError,
      '--kpi-error-bg': token.colorErrorBg,
      '--kpi-error-border': token.colorErrorBorder,
      '--kpi-primary': token.colorPrimary,
      '--kpi-border': token.colorBorderSecondary,
      '--kpi-fill-quaternary': token.colorFillQuaternary,
      '--kpi-checkpoint-border': checkpointBorderColor,
    }

    return {
      effectiveHealth,
      effectiveTrend,
      health,
      trendArrow,
      hasActual,
      cssVars,
    }
  })()

  return (
    <Card
      size="small"
      hoverable={!!onPress}
      aria-label={`KPI ${data.key}: ${data.name}, ${effectiveHealth ?? 'No Data'}`}
      onClick={() => onPress?.(data.id)}
      className={`${styles.card}${onPress ? ` ${styles.cardClickable}` : ''}`}
      style={cssVars}
      styles={{ body: cardBodyStyles }}
    >
      <div className={styles.accentBar} />

      <Flex vertical gap={10} className={styles.body}>
        {/* Header */}
        <Flex vertical gap={4}>
          <Flex justify="space-between" align="center" gap={6} wrap="nowrap">
            <Flex align="center" gap={6} wrap="nowrap">
              <Text className={styles.key}>#{data.key}</Text>
              <DirectionBadge direction={data.targetDirection} />
            </Flex>
            <HealthBadge health={effectiveHealth} token={token} />
          </Flex>
          <span className={styles.name} title={data.name}>
            {data.name}
          </span>
        </Flex>

        {/* Values row */}
        <Flex align="flex-end" gap={0}>
          <Flex vertical gap={2} className={styles.actualSegment}>
            <Text className={styles.valueLabel}>ACTUAL</Text>
            <Flex align="baseline" gap={4}>
              <span className={styles.actualValue}>
                {hasActual ? formatValue(data.actualValue!, data.prefix, data.suffix) : '—'}
              </span>
              {hasActual && trendArrow && (
                <span aria-hidden="true" className={styles.trendArrow}>
                  {trendArrow}
                </span>
              )}
            </Flex>
          </Flex>

          <Flex vertical gap={2} className={styles.targetSegment}>
            <Text className={styles.valueLabel}>TARGET</Text>
            <span className={styles.targetValue}>
              {formatValue(data.targetValue, data.prefix, data.suffix)}
            </span>
          </Flex>
        </Flex>

        {/* Trend label */}
        {effectiveTrend && effectiveTrend !== KpiTrend.NoData && (
          <Text className={styles.trendLabel}>
            <span aria-hidden="true" className={styles.trendArrowChar}>
              {trendArrow}
            </span>
            {getTrendLabel(effectiveTrend)} · since last checkpoint
          </Text>
        )}

        {/* Progress section */}
        <ProgressSection data={data} healthColor={health.text} />

        {/* Checkpoint strip */}
        {checkpointLoading ? (
          <SkeletonInput active className={styles.checkpointSkeleton} />
        ) : checkpoint ? (
          <CheckpointStrip checkpoint={checkpoint} prefix={data.prefix} suffix={data.suffix} token={token} />
        ) : null}

        {/* Footer */}
        <Flex justify="space-between" align="center" className={styles.footer}>
          <Text className={styles.footerMeta}>
            {hasActual ? 'Measured' : 'Never measured'}
          </Text>
          {nextCheckpoint ? (
            <Text className={styles.footerNext}>
              Next checkpoint: {nextCheckpoint.label} →{' '}
              {formatValue(nextCheckpoint.targetValue, data.prefix, data.suffix)}
            </Text>
          ) : (
            <Text className={styles.footerMeta}>No checkpoints</Text>
          )}
        </Flex>
      </Flex>
    </Card>
  )
}

// ─── Add KPI Card ─────────────────────────────────────────────────────────────

interface AddKpiCardProps {
  onClick: () => void
}

export const AddKpiCard: FC<AddKpiCardProps> = ({ onClick }) => {
  const { token } = useTheme()

  const cssVars = {
    '--kpi-add-border': token.colorBorderSecondary,
    '--kpi-add-bg': token.colorFillQuaternary,
    '--kpi-add-color': token.colorTextTertiary,
  } as CSSProperties

  return (
    <Card onClick={onClick} className={styles.addCard} style={cssVars}>
      <Flex vertical align="center" gap={8}>
        <PlusCircleOutlined className={styles.addIcon} />
        <Text className={styles.addLabel}>Add KPI</Text>
      </Flex>
    </Card>
  )
}

export default KpiCard
