'use client'

import { WaydEmpty } from '@/src/components/common'
import StageTimeline from '@/src/app/ppm/_components/stage-timeline'
import ProjectTaskMetrics from '@/src/app/ppm/projects/_components/project-task-metrics'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { useGetProjectPlanSummaryQuery } from '@/src/store/features/ppm/projects-api'
import { Card, Flex } from 'antd'
import Block from './overview-block'
import ProjectDeliverablesSummary from './project-deliverables-summary'

export interface ProjectOverviewProps {
  project: ProjectDetailsDto
}

/**
 * How the project is progressing: its stages, what it has committed to
 * deliver, and the tasks inside the stages.
 *
 * A project with no lifecycle has no stages to show, which is a setup gap
 * rather than an empty result — hence a message pointing at the cause rather
 * than a blank panel.
 */
const ProjectOverview = ({ project }: ProjectOverviewProps) => {
  const { data: planSummary } = useGetProjectPlanSummaryQuery(
    { projectKey: project.key },
    { skip: !project.stages?.length },
  )

  const hasTasks = (planSummary?.totalLeafTasks ?? 0) > 0

  // Deliverables are usually agreed before a lifecycle is assigned, so they
  // show either way.
  const deliverables = (
    <ProjectDeliverablesSummary
      projectId={project.id}
      projectKey={project.key}
      style={{ flex: '1 1 420px', minWidth: 0 }}
    />
  )

  if (!project.projectLifecycle) {
    return (
      <Flex vertical gap="large">
        {deliverables}
        <WaydEmpty message="No lifecycle defined for this project." />
      </Flex>
    )
  }

  return (
    <Flex vertical gap="large">
      {project.stages?.length > 0 && (
        <Block title="Stages">
          <StageTimeline stages={project.stages} />
        </Block>
      )}

      {/* Side by side on wide screens; each wraps to full width on narrow ones. */}
      <Flex gap="middle" wrap align="stretch">
        {deliverables}
        {hasTasks && (
          <Card
            size="small"
            title="Task Summary"
            style={{ flex: '1 1 420px', minWidth: 0 }}
          >
            <ProjectTaskMetrics projectKey={project.key} />
          </Card>
        )}
      </Flex>
    </Flex>
  )
}

export default ProjectOverview
