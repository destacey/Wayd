'use client'

import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { useGetProjectDeliverablesQuery } from '@/src/store/features/ppm/project-deliverables-api'
import { useGetProjectPlanTreeQuery } from '@/src/store/features/ppm/projects-api'
import { Segmented } from 'antd'
import { FC, useState } from 'react'
import { ProjectPlanTable } from '.'
import ProjectPlanByDeliverable from './project-plan-by-deliverable'

export interface ProjectPlanProps {
  project: ProjectDetailsDto
  canManageTasks: boolean
}

type PlanGrouping = 'stage' | 'deliverable'

const GROUPING_OPTIONS: { label: string; value: PlanGrouping }[] = [
  { label: 'By Stage', value: 'stage' },
  { label: 'By Deliverable', value: 'deliverable' },
]

/**
 * The project's plan, either as its work breakdown by stage, where tasks are
 * edited, or regrouped by the deliverables the work serves.
 */
const ProjectPlan: FC<ProjectPlanProps> = ({
  project,
  canManageTasks,
}: ProjectPlanProps) => {
  const [grouping, setGrouping] = useState<PlanGrouping>('stage')

  const {
    data: planData,
    isLoading,
    refetch,
  } = useGetProjectPlanTreeQuery(project?.key || '', {
    skip: !project?.key || !project?.projectLifecycle,
  })
  const {
    data: deliverables = [],
    isLoading: deliverablesLoading,
    refetch: refetchDeliverables,
  } = useGetProjectDeliverablesQuery(project?.id ?? '', {
    skip: !project?.id || grouping !== 'deliverable',
  })

  if (!project) return null

  const groupingSelector = (
    <Segmented<PlanGrouping>
      size="small"
      options={GROUPING_OPTIONS}
      value={grouping}
      onChange={setGrouping}
      aria-label="Group the plan"
    />
  )

  if (grouping === 'deliverable') {
    return (
      <ProjectPlanByDeliverable
        projectKey={project.key}
        plan={planData ?? []}
        deliverables={deliverables}
        isLoading={isLoading || deliverablesLoading}
        onRefresh={() => {
          refetch()
          refetchDeliverables()
        }}
        leftSlot={groupingSelector}
      />
    )
  }

  return (
    <ProjectPlanTable
      projectId={project.id}
      projectKey={project.key}
      tasks={planData ?? []}
      isLoading={isLoading}
      canManageTasks={canManageTasks}
      refetch={refetch}
      leftSlot={groupingSelector}
    />
  )
}

export default ProjectPlan
