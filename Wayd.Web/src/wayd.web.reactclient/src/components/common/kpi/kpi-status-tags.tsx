'use client'

import { KpiHealth, KpiTrend } from '@/src/services/api/wayd.schemas'
import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  MinusOutlined,
} from '@ant-design/icons'
import { Tag } from 'antd'
import { FC } from 'react'

/**
 * A KPI's health against its target: Healthy, At Risk or Unhealthy. Nothing
 * when it has not been measured.
 */
export const KpiHealthTag: FC<{ health: KpiHealth | null | undefined }> = ({
  health,
}) => {
  if (!health) return null
  if (health === KpiHealth.Healthy)
    return (
      <Tag icon={<CheckCircleOutlined />} color="success">
        Healthy
      </Tag>
    )
  if (health === KpiHealth.AtRisk)
    return (
      <Tag icon={<ExclamationCircleOutlined />} color="warning">
        At Risk
      </Tag>
    )
  return (
    <Tag icon={<CloseCircleOutlined />} color="error">
      Unhealthy
    </Tag>
  )
}

/**
 * Whether a KPI's latest value moved the right way from the one before it.
 * Nothing when there is no earlier value to compare with.
 */
export const KpiTrendTag: FC<{ trend: KpiTrend | null | undefined }> = ({
  trend,
}) => {
  if (!trend || trend === KpiTrend.NoData) return null
  if (trend === KpiTrend.Improving)
    return (
      <Tag icon={<ArrowUpOutlined />} color="success">
        Improving
      </Tag>
    )
  if (trend === KpiTrend.Worsening)
    return (
      <Tag icon={<ArrowDownOutlined />} color="error">
        Worsening
      </Tag>
    )
  return (
    <Tag icon={<MinusOutlined />} color="default">
      Stable
    </Tag>
  )
}
