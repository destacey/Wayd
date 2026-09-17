'use client'

import { MetricCard } from '@/src/components/common/metrics'
import { CutToReleasedDto, ReleaseFrequencyDto } from '@/src/services/wayd-api'
import { ArrowDownOutlined, ArrowUpOutlined } from '@ant-design/icons'
import { Col, Flex, Row, Typography } from 'antd'

const { Text } = Typography

/**
 * The change against the window immediately before this one.
 *
 * Renders nothing comparative when there is no prior figure. A missing baseline is not a rise from
 * zero, and showing one would invent a trend out of a product's first fortnight.
 */
const Delta = ({
  current,
  previous,
  suffix,
  lowerIsBetter = false,
}: {
  current: number | null | undefined
  previous: number | null | undefined
  suffix: string
  lowerIsBetter?: boolean
}) => {
  if (current == null || previous == null) {
    return <Text type="secondary">No earlier window to compare</Text>
  }

  const change = current - previous
  if (Math.abs(change) < 0.005) {
    return <Text type="secondary">Unchanged</Text>
  }

  const rose = change > 0
  const good = lowerIsBetter ? !rose : rose

  return (
    <Text type={good ? 'success' : 'warning'}>
      {rose ? <ArrowUpOutlined /> : <ArrowDownOutlined />}{' '}
      {Math.abs(change).toFixed(1)}
      {suffix} vs previous
    </Text>
  )
}

/**
 * The line under a metric: what it is made of, then how it moved.
 *
 * Both belong on the card. The parts are what let two windows be combined by summing rather than
 * averaging the rates, and the change is what makes a single figure mean anything.
 */
const Supporting = ({
  parts,
  delta,
}: {
  parts: string
  delta: React.ReactNode
}) => (
  <Flex vertical align="flex-end" style={{ fontSize: 12 }}>
    <Text type="secondary">{parts}</Text>
    {delta}
  </Flex>
)

export interface DeliveryOverviewTilesProps {
  frequency: ReleaseFrequencyDto
  cutToReleased: CutToReleasedDto
}

/**
 * Cadence and speed — the two things version records can honestly measure.
 *
 * Neither says anything about whether a deployment worked. That is the deployment record's job, and
 * the delivery measures read it on their own page.
 */
const DeliveryOverviewTiles = ({
  frequency,
  cutToReleased,
}: DeliveryOverviewTilesProps) => (
  <Row gutter={[16, 16]}>
    <Col xs={24} sm={12}>
      <MetricCard
        title="Versions released"
        tooltip="Versions marked released in the window, as a weekly rate. Counts what was shipped, not what reached an environment."
        value={frequency.perWeek.toFixed(1)}
        suffix="/ week"
        secondaryValue={
          <Supporting
            parts={`${frequency.count} version${frequency.count === 1 ? '' : 's'} over ${frequency.windowDays} days`}
            delta={
              <Delta
                current={frequency.perWeek}
                previous={frequency.previousPerWeek}
                suffix=" / week"
              />
            }
          />
        }
      />
    </Col>

    <Col xs={24} sm={12}>
      <MetricCard
        title="Cut to released"
        tooltip="Mean days from cutting a version to releasing it. Versions released without ever being cut carry no latency and are left out rather than counted as zero."
        value={
          cutToReleased.averageDays != null
            ? cutToReleased.averageDays.toFixed(1)
            : '—'
        }
        suffix={cutToReleased.averageDays != null ? 'days' : undefined}
        secondaryValue={
          <Supporting
            parts={`${cutToReleased.measuredCount} of ${cutToReleased.releasedCount} version${cutToReleased.releasedCount === 1 ? '' : 's'} measurable`}
            delta={
              <Delta
                current={cutToReleased.averageDays}
                previous={cutToReleased.previousAverageDays}
                suffix=" days"
                lowerIsBetter
              />
            }
          />
        }
      />
    </Col>
  </Row>
)

export default DeliveryOverviewTiles
