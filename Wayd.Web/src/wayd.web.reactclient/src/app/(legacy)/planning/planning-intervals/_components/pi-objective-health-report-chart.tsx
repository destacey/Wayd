'use client'

import { FC } from 'react'
import { useGetObjectiveHealthChecksQuery } from '@/src/store/features/planning/pi-objective-health-checks-api'
import HealthStatusHistoryChart from '@/src/components/common/health-check/health-status-history-chart'

interface PiObjectiveHealthReportChartProps {
  planningIntervalId: string
  objectiveId: string
}

const PiObjectiveHealthReportChart: FC<PiObjectiveHealthReportChartProps> = (
  props: PiObjectiveHealthReportChartProps,
) => {
  const { data: healthReportData, isLoading } =
    useGetObjectiveHealthChecksQuery(
      {
        planningIntervalId: props.planningIntervalId,
        objectiveId: props.objectiveId,
      },
      { skip: !props.planningIntervalId || !props.objectiveId },
    )

  return (
    <HealthStatusHistoryChart
      data={healthReportData}
      isLoading={isLoading}
      cardStyle={{ width: 375 }}
    />
  )
}

export default PiObjectiveHealthReportChart

