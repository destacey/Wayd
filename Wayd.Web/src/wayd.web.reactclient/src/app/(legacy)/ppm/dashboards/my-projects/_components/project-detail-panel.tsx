'use client'

import { WaydEmpty } from '@/src/components/common'
import StageTimeline from '@/src/app/(legacy)/ppm/_components/stage-timeline'
import ProjectTaskMetrics from '@/src/app/(legacy)/ppm/projects/_components/project-task-metrics'
import { useGetProjectQuery } from '@/src/store/features/ppm/projects-api'
import { Card, Flex, Skeleton, Typography } from 'antd'
import EntityLink from '@/src/components/common/entity-link'
import { FC } from 'react'
import ProjectDetailHeader from './project-detail-header'
import ProjectPlanView from './project-plan-view'
import styles from '../my-projects-dashboard.module.css'

const { Text } = Typography

export interface ProjectDetailPanelProps {
  projectKey: string | null
}

const ProjectDetailPanel: FC<ProjectDetailPanelProps> = ({ projectKey }) => {
  if (!projectKey) {
    return (
      <div className={styles.detailEmpty}>
        <Text type="secondary">Select a project to view details</Text>
      </div>
    )
  }

  return (
    <Card size="small" key={projectKey}>
      <ProjectDetailContent projectKey={projectKey} />
    </Card>
  )
}

const ProjectDetailContent: FC<{ projectKey: string }> = ({ projectKey }) => {
  const { data: project, isLoading } = useGetProjectQuery(projectKey)

  if (isLoading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (!project) {
    return (
      <div className={styles.detailEmpty}>
        <Text type="secondary">Project not found</Text>
      </div>
    )
  }

  const hasLifecycle = !!project.projectLifecycle

  return (
    <Flex vertical gap={0}>
      <ProjectDetailHeader project={project} />

      {hasLifecycle ? (
        <>
          {project.stages?.length > 0 && (
            <Flex vertical gap={8} className={styles.detailSection}>
              <Text strong style={{ fontSize: 13 }}>Stages</Text>
              <StageTimeline stages={project.stages} />
            </Flex>
          )}

          <Flex vertical gap={8} className={styles.detailSection}>
            <Text strong style={{ fontSize: 13 }}>Task Summary</Text>
            <ProjectTaskMetrics projectKey={project.key} />
          </Flex>

          <Flex vertical gap={8}>
            <div>
              <EntityLink
                href={`/ppm/projects/${project.key}?section=plan`}
                style={{ fontSize: 13 }}
              >
                Project Plan
              </EntityLink>
            </div>
            <ProjectPlanView projectKey={project.key} />
          </Flex>
        </>
      ) : (
        <WaydEmpty message="No project plan defined. Assign a project lifecycle to enable planning." />
      )}
    </Flex>
  )
}

export default ProjectDetailPanel
