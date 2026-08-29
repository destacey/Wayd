'use client'

import PiObjectiveHealthReportChart from '@/src/app/planning/planning-intervals/_components/pi-objective-health-report-chart'
import { WaydTooltip } from '@/src/components/common'
import { PlanningIntervalObjectiveDetailsDto } from '@/src/services/wayd-api'
import { Flex, Progress } from 'antd'
import PlanningIntervalObjectiveWorkItemsCard from './planning-interval-objective-work-items-card'

export interface PlanningIntervalObjectiveOverviewProps {
  objective: PlanningIntervalObjectiveDetailsDto
  canManageObjectives: boolean
}

/**
 * How the objective is tracking: progress to date, health over time, and the
 * work carrying it.
 *
 * Its attributes live in the facts panel, so what remains here is the movement
 * rather than the record's stable description.
 */
const PlanningIntervalObjectiveOverview = ({
  objective,
  canManageObjectives,
}: PlanningIntervalObjectiveOverviewProps) => {
  const progressStatus = ['Canceled', 'Missed'].includes(objective.status?.name)
    ? 'exception'
    : undefined

  return (
    <Flex vertical gap="middle">
      <WaydTooltip title="Progress">
        <Progress percent={objective.progress} status={progressStatus} />
      </WaydTooltip>

      {/* Two thirds to the work, one to its health. Both shrink with the row
          and wrap rather than overflow, so neither is left stranded beside a
          fixed-width sibling. */}
      <Flex gap="middle" align="start" wrap>
        <div style={{ flex: '2 1 420px', minWidth: 300 }}>
          <PlanningIntervalObjectiveWorkItemsCard
            planningIntervalKey={objective.planningInterval?.key}
            objectiveKey={objective.key}
            canLinkWorkItems={canManageObjectives}
          />
        </div>
        <div style={{ flex: '1 1 210px', minWidth: 280 }}>
          <PiObjectiveHealthReportChart
            planningIntervalId={objective.planningInterval?.id}
            objectiveId={objective.id}
            cardStyle={{ width: '100%' }}
          />
        </div>
      </Flex>
    </Flex>
  )
}

export default PlanningIntervalObjectiveOverview
