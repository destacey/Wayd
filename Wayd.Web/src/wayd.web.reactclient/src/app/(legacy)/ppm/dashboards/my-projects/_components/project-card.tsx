'use client'

import { LifecycleStatusTag } from '@/src/components/common'
import StageTimeline from '@/src/app/(legacy)/ppm/_components/stage-timeline'
import ProjectHealthCheckTag from '@/src/app/(legacy)/ppm/projects/_components/project-health-check-tag'
import { ProjectListDto, ProjectPlanSummaryDto } from '@/src/services/wayd-api'
import { Card, Flex, Typography } from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'
import { collectTeamMembers, getUserRoles } from './project-card-helpers'
import ProjectStatPills from './project-stat-pills'
import TeamAvatars from './team-avatars'
import styles from '../my-projects-dashboard.module.css'

const { Text } = Typography

export interface ProjectCardProps {
  project: ProjectListDto
  isSelected: boolean
  employeeId: string | null
  planSummary?: ProjectPlanSummaryDto
  onSelect: (key: string) => void
}

const ProjectCard: FC<ProjectCardProps> = ({
  project,
  isSelected,
  employeeId,
  planSummary,
  onSelect,
}) => {
  const roles = getUserRoles(project, employeeId)
  const teamMembers = collectTeamMembers(project)
  const endDate = project.end ? dayjs(project.end).format('MMM D, YYYY') : null

  return (
    <Card
      size="small"
      hoverable
      className={isSelected ? styles.projectCardSelected : undefined}
      onClick={() => onSelect(project.key)}
    >
      <Flex vertical gap={10}>
        {/* Header row: role + status */}
        <Flex vertical gap={4}>
          <Flex
            align="center"
            justify={roles.length > 0 ? 'space-between' : 'flex-end'}
          >
            {roles.length > 0 && (
              <Text type="secondary" className={styles.roleBadge}>
                {roles.join(' · ')}
              </Text>
            )}
            <Flex align="center" gap="small">
              <ProjectHealthCheckTag
                healthCheck={project.healthCheck}
                projectId={project.id}
                variant="flag"
              />
              <LifecycleStatusTag status={project.status} />
            </Flex>
          </Flex>
          <Text type="secondary" style={{ fontSize: 11 }}>
            {project.key}
          </Text>
          <Text className={styles.projectName} ellipsis>
            {project.name}
          </Text>
        </Flex>

        {/* Stage timeline */}
        {project.stages?.length > 0 && (
          <StageTimeline stages={project.stages} displayMode="small" />
        )}

        {/* Stat pills */}
        <ProjectStatPills summary={planSummary} />

        {/* Footer: avatars + end date */}
        <div className={styles.cardFooter}>
          <TeamAvatars members={teamMembers} />
          {endDate && <span className={styles.endDate}>End {endDate}</span>}
        </div>
      </Flex>
    </Card>
  )
}

export default ProjectCard
