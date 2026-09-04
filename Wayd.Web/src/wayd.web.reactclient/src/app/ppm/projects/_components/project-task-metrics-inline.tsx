'use client'

import { WaydTooltip } from '@/src/components/common'
import { useGetProjectPlanSummaryQuery } from '@/src/store/features/ppm/projects-api'
import { FC } from 'react'
import styles from './project-task-metrics-inline.module.css'

export interface ProjectTaskMetricsInlineProps {
  projectKey: string
  employeeId?: string
}

const CountItem: FC<{
  value: number
  label: string
  valueClassName?: string
  tooltip: string
}> = ({ value, label, valueClassName, tooltip }) => (
  <WaydTooltip title={tooltip}>
    <span>
      <span className={`${styles.countValue} ${valueClassName ?? ''}`}>
        {value}
      </span>{' '}
      {label}
    </span>
  </WaydTooltip>
)

/**
 * The plan summary condensed onto the section heading row, where the metric
 * cards would not fit. Same figures and wording as ProjectTaskMetrics.
 */
const ProjectTaskMetricsInline: FC<ProjectTaskMetricsInlineProps> = ({
  projectKey,
  employeeId,
}) => {
  const { data: summary, isLoading } = useGetProjectPlanSummaryQuery({
    projectKey,
    employeeId,
  })

  if (isLoading || !summary || summary.totalLeafTasks === 0) return null

  return (
    <div className={styles.summary}>
      <CountItem
        value={summary.overdue}
        label="overdue"
        valueClassName={summary.overdue > 0 ? styles.countValueDanger : ''}
        tooltip="Tasks past their end date that are Not Started or In Progress."
      />
      <CountItem
        value={summary.dueThisWeek}
        label="due this week"
        valueClassName={summary.dueThisWeek > 0 ? styles.countValueWarning : ''}
        tooltip="Tasks due by Saturday that are Not Started or In Progress."
      />
      <CountItem
        value={summary.upcoming}
        label="upcoming"
        tooltip="Tasks due next week that are Not Started or In Progress."
      />
    </div>
  )
}

export default ProjectTaskMetricsInline
