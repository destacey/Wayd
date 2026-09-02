'use client'

import {
  ChangeFailureRateDto,
  DeploymentFrequencyDto,
} from '@/src/services/wayd-api'
import { Card, Col, Row, Statistic, Tooltip, Typography } from 'antd'

const { Text } = Typography

export interface DeliveryMetricTilesProps {
  deploymentFrequency: DeploymentFrequencyDto
  changeFailureRate: ChangeFailureRateDto
}

/**
 * The two measures this module can compute.
 *
 * Each tile carries its count and total beside the rate, so windows can be combined by summing the
 * parts rather than averaging the rates — an average of averages weights a quiet week the same as a
 * busy one.
 *
 * A null rate is "no deployments to judge", which is not a rate of zero: zero would claim nothing
 * failed, when in fact nothing shipped. The DTO distinguishes them and so does this.
 */
const DeliveryMetricTiles = ({
  deploymentFrequency,
  changeFailureRate,
}: DeliveryMetricTilesProps) => (
  <Row gutter={[16, 16]}>
    <Col xs={24} md={12}>
      <Card>
        <Statistic
          title={
            <Tooltip title="Completed production deployments per day. A rolled-back deployment still counts — it reached production, which is what this measures.">
              <span>Deployment Frequency</span>
            </Tooltip>
          }
          value={
            deploymentFrequency.perDay != null
              ? deploymentFrequency.perDay.toFixed(2)
              : '—'
          }
          suffix={deploymentFrequency.perDay != null ? '/ day' : undefined}
        />
        <Text type="secondary">
          {deploymentFrequency.count} deployment
          {deploymentFrequency.count === 1 ? '' : 's'} over{' '}
          {deploymentFrequency.windowDays.toFixed(1)} days
        </Text>
      </Card>
    </Col>

    <Col xs={24} md={12}>
      <Card>
        <Statistic
          title={
            <Tooltip title="A rollback-derived proxy: the share of production deployments that failed or were rolled back. Not the full DORA measure, which counts every change that degraded service however it was resolved.">
              <span>Change Failure Rate (proxy)</span>
            </Tooltip>
          }
          value={
            changeFailureRate.rate != null
              ? `${(changeFailureRate.rate * 100).toFixed(1)}%`
              : 'No deployments to judge'
          }
        />
        <Text type="secondary">
          {changeFailureRate.failedDeployments} of{' '}
          {changeFailureRate.totalDeployments} production deployment
          {changeFailureRate.totalDeployments === 1 ? '' : 's'}
        </Text>
      </Card>
    </Col>
  </Row>
)

export default DeliveryMetricTiles
