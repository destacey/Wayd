'use client'

import { WaydEmpty } from '@/src/components/common'
import StageTimeline from '@/src/app/ppm/_components/stage-timeline'
import ProjectTaskMetrics from '@/src/app/ppm/projects/_components/project-task-metrics'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { useGetProjectPlanSummaryQuery } from '@/src/store/features/ppm/projects-api'
import { Flex, Typography } from 'antd'

const { Text } = Typography

export interface ProjectOverviewProps {
  project: ProjectDetailsDto
}

/**
 * A titled block on the overview.
 *
 * 14px `Text strong` — the third step of the type scale, below the section
 * heading the layout renders above it.
 */
const Block = ({
  title,
  children,
}: {
  title: string
  children: React.ReactNode
}) => (
  <Flex vertical gap="small">
    <Text strong style={{ fontSize: 14 }}>
      {title}
    </Text>
    {children}
  </Flex>
)

/**
 * How the project is progressing: its stages, and the tasks inside them.
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

  if (!project.projectLifecycle) {
    return <WaydEmpty message="No lifecycle defined for this project." />
  }

  return (
    <Flex vertical gap="large">
      {project.stages?.length > 0 && (
        <Block title="Stages">
          <StageTimeline stages={project.stages} />
        </Block>
      )}

      {hasTasks && (
        <Block title="Task Summary">
          <ProjectTaskMetrics projectKey={project.key} />
        </Block>
      )}
    </Flex>
  )
}

export default ProjectOverview
