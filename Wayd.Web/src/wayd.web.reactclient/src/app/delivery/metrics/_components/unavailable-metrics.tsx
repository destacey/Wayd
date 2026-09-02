'use client'

import { UnavailableMetricDto } from '@/src/services/wayd-api'
import { WaydList } from '@/src/components/common'
import { Card, Typography } from 'antd'

const { Text } = Typography

export interface UnavailableMetricsProps {
  unavailable: UnavailableMetricDto[]
}

/**
 * The measures this module does not compute, each with the server's reason.
 *
 * Rendered from the array rather than hardcoded: which measures are missing is the server's to say,
 * and a hardcoded pair would go stale the moment one of them ships. Shown rather than omitted so a
 * reader can tell "we do not measure this yet" from "nothing deployed".
 */
const UnavailableMetrics = ({ unavailable }: UnavailableMetricsProps) => {
  if (unavailable.length === 0) return null

  return (
    <Card title="Not measured yet" size="small">
      <WaydList
        dataSource={unavailable}
        renderItem={(metric: UnavailableMetricDto) => (
          <WaydList.Item key={metric.metric}>
            <WaydList.Item.Meta
              title={metric.metric}
              description={<Text type="secondary">{metric.reason}</Text>}
            />
          </WaydList.Item>
        )}
      />
    </Card>
  )
}

export default UnavailableMetrics
